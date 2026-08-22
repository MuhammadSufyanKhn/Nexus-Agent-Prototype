using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nexus.Data.LLM;

public class LocalLLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly LLMOptions _options;
    private readonly ILogger<LocalLLMService> _logger;

    public LocalLLMService(HttpClient httpClient, IOptions<LLMOptions> options, ILogger<LocalLLMService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private string GetResolvedBaseUrl()
    {
        var url = _options.BaseUrl ?? "http://127.0.0.1:11434";
        return url.Replace("localhost", "127.0.0.1").TrimEnd('/');
    }

    public async Task<LLMResponse> GenerateCompletionAsync(string prompt, string? systemPrompt = null, bool requestJson = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return LLMResponse.Failure("Prompt cannot be empty.", _options.Model);
        }

        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Min(_options.TimeoutSeconds, 10)));

        var baseUrl = GetResolvedBaseUrl();

        try
        {
            // 1. Try Ollama native endpoint (/api/generate)
            var ollamaPayload = new
            {
                model = _options.Model,
                prompt = prompt,
                system = systemPrompt,
                stream = false,
                format = requestJson ? "json" : (string?)null
            };

            var requestJsonString = JsonSerializer.Serialize(ollamaPayload);
            var content = new StringContent(requestJsonString, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{baseUrl}/api/generate", content, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                string completionText = string.Empty;
                if (root.TryGetProperty("response", out var respProp))
                {
                    completionText = respProp.GetString() ?? string.Empty;
                }

                sw.Stop();
                return LLMResponse.Success(completionText, _options.Model, sw.ElapsedMilliseconds);
            }

            // 2. Fallback to OpenAI-compatible local endpoint (/v1/chat/completions)
            _logger.LogInformation("Ollama native endpoint failed, trying local OpenAI-compatible endpoint.");

            var openAiPayload = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt ?? "You are a helpful AI business process assistant." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.2
            };

            var openAiContent = new StringContent(JsonSerializer.Serialize(openAiPayload), Encoding.UTF8, "application/json");
            var openAiResponse = await _httpClient.PostAsync($"{baseUrl}/v1/chat/completions", openAiContent, cts.Token);

            if (openAiResponse.IsSuccessStatusCode)
            {
                var openAiBody = await openAiResponse.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(openAiBody);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var text = choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                    sw.Stop();
                    return LLMResponse.Success(text, _options.Model, sw.ElapsedMilliseconds);
                }
            }

            sw.Stop();
            return LLMResponse.Failure($"Local LLM server returned HTTP {response.StatusCode} on {baseUrl}.", _options.Model);
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            _logger.LogWarning("Local LLM call timed out after timeout period.");
            return LLMResponse.Failure($"Local LLM request timed out.", _options.Model);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Failed to connect to local LLM runtime at {BaseUrl}.", baseUrl);
            return LLMResponse.Failure($"Local LLM runtime unavailable at {baseUrl}.", _options.Model);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Unexpected error calling local LLM.");
            return LLMResponse.Failure($"Unexpected local LLM error: {ex.Message}", _options.Model);
        }
    }

    public async Task<T?> GenerateJsonAsync<T>(string prompt, string? systemPrompt = null, CancellationToken cancellationToken = default)
    {
        var jsonSystemPrompt = (systemPrompt ?? string.Empty) + "\nIMPORTANT: Respond ONLY with valid JSON. Do not include markdown formatting or extra text.";

        var response = await GenerateCompletionAsync(prompt, jsonSystemPrompt, requestJson: true, cancellationToken);
        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Content))
        {
            return default;
        }

        try
        {
            var rawText = SanitizeJsonOutput(response.Content);
            return JsonSerializer.Deserialize<T>(rawText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response from local LLM output: {Content}", response.Content);
            return default;
        }
    }

    public async Task<LLMHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var rawUrl = _options.BaseUrl ?? "http://127.0.0.1:11434";
        var urls = new[] { GetResolvedBaseUrl(), rawUrl, rawUrl.Replace("127.0.0.1", "localhost") };

        foreach (var baseUrl in urls.Where(u => !string.IsNullOrEmpty(u)).Distinct())
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(2));

                var response = await _httpClient.GetAsync($"{baseUrl}/api/tags", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    sw.Stop();
                    return new LLMHealthStatus
                    {
                        IsAvailable = true,
                        Provider = _options.Provider,
                        Model = _options.Model,
                        BaseUrl = baseUrl,
                        ResponseTimeMs = sw.ElapsedMilliseconds
                    };
                }

                var rootResp = await _httpClient.GetAsync($"{baseUrl}/", cts.Token);
                if (rootResp.IsSuccessStatusCode)
                {
                    sw.Stop();
                    return new LLMHealthStatus
                    {
                        IsAvailable = true,
                        Provider = _options.Provider,
                        Model = _options.Model,
                        BaseUrl = baseUrl,
                        ResponseTimeMs = sw.ElapsedMilliseconds
                    };
                }
            }
            catch
            {
                // Continue checking next URL candidate
            }
        }

        sw.Stop();
        return new LLMHealthStatus
        {
            IsAvailable = false,
            Provider = _options.Provider,
            Model = _options.Model,
            BaseUrl = _options.BaseUrl ?? "http://127.0.0.1:11434",
            ResponseTimeMs = sw.ElapsedMilliseconds,
            ErrorMessage = "Local LLM server is unreachable."
        };
    }

    private static string SanitizeJsonOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";

        var trimmed = text.Trim();

        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(7);
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed.Substring(3);
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
        }

        trimmed = trimmed.Trim();

        var firstOpen = trimmed.IndexOf('{');
        var lastClose = trimmed.LastIndexOf('}');

        if (firstOpen >= 0 && lastClose > firstOpen)
        {
            return trimmed.Substring(firstOpen, lastClose - firstOpen + 1);
        }

        return trimmed;
    }
}
