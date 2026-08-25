using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Data.LLM;
using Nexus.Tools.Core;

namespace Nexus.Agent.LLM;

/// <summary>
/// Service that implements native Gemini API Tool Calling (Function Calling).
/// Wraps registered IAgentTool schemas into Gemini tools payload and parses
/// returned functionCall objects natively.
/// </summary>
public class GeminiFunctionCallingService
{
    private const string GeminiBaseUrl = "https://generativelanguage.googleapis.com";
    private readonly HttpClient _httpClient;
    private readonly LLMOptions _options;
    private readonly ILogger<GeminiFunctionCallingService> _logger;

    public GeminiFunctionCallingService(
        HttpClient httpClient,
        IOptions<LLMOptions> options,
        ILogger<GeminiFunctionCallingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(GeminiBaseUrl);
    }

    public async Task<FunctionCallResponse> CallWithToolsAsync(
        string prompt,
        IEnumerable<ToolDefinition> tools,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var apiKey = _options.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new FunctionCallResponse
            {
                HasFunctionCall = false,
                ErrorMessage = "Gemini API key is not configured."
            };
        }

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-2.0-flash" : _options.Model;

        // Build Gemini function declarations from ToolDefinition schemas
        var functionDeclarations = tools.Select(t =>
        {
            object parametersObj;
            try
            {
                using var doc = JsonDocument.Parse(t.InputSchema);
                parametersObj = doc.RootElement.Clone();
            }
            catch
            {
                parametersObj = new { type = "object", properties = new { } };
            }

            return new
            {
                name = t.Name.Replace('.', '_'), // Gemini function name identifier
                description = t.Description,
                parameters = parametersObj
            };
        }).ToList();

        var fullPrompt = prompt;
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            fullPrompt = $"SYSTEM: {systemPrompt}\n\nUSER: {prompt}";
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = fullPrompt }
                    }
                }
            },
            tools = new[]
            {
                new
                {
                    functionDeclarations = functionDeclarations
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                topK = 40,
                topP = 0.95,
                maxOutputTokens = 2048
            }
        };

        try
        {
            var bodyJson = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            var endpoint = $"/v1beta/models/{model}:generateContent?key={apiKey}";

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini Function Calling API error HTTP {Status}: {Body}", (int)response.StatusCode, responseBody);
                return new FunctionCallResponse
                {
                    HasFunctionCall = false,
                    ExecutionTimeMs = sw.ElapsedMilliseconds,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}"
                };
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var contentProp) &&
                    contentProp.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        // Check for native Gemini functionCall property
                        if (part.TryGetProperty("functionCall", out var functionCall))
                        {
                            var rawFnName = functionCall.GetProperty("name").GetString() ?? "";
                            var canonicalName = rawFnName.Replace('_', '.'); // Convert back to tool registry name (e.g. employee_create -> employee.create)

                            var argsDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            if (functionCall.TryGetProperty("args", out var argsElement))
                            {
                                foreach (var prop in argsElement.EnumerateObject())
                                {
                                    argsDict[prop.Name] = prop.Value.ValueKind switch
                                    {
                                        JsonValueKind.String => prop.Value.GetString() ?? "",
                                        JsonValueKind.Number => prop.Value.TryGetDecimal(out var d) ? d : prop.Value.GetDouble(),
                                        JsonValueKind.True => true,
                                        JsonValueKind.False => false,
                                        _ => prop.Value.ToString()
                                    };
                                }
                            }

                            _logger.LogInformation("Native Gemini Function Call Detected: {FnName} with {Count} args", canonicalName, argsDict.Count);

                            return new FunctionCallResponse
                            {
                                HasFunctionCall = true,
                                FunctionName = canonicalName,
                                Arguments = argsDict,
                                Model = model,
                                ExecutionTimeMs = sw.ElapsedMilliseconds
                            };
                        }
                    }

                    // Fallback to text response if no function call returned
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var textProp))
                    {
                        return new FunctionCallResponse
                        {
                            HasFunctionCall = false,
                            TextResponse = textProp.GetString(),
                            Model = model,
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }
                }
            }

            return new FunctionCallResponse
            {
                HasFunctionCall = false,
                Model = model,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Gemini Function Calling exception");
            return new FunctionCallResponse
            {
                HasFunctionCall = false,
                ErrorMessage = ex.Message,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
    }
}
