using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nexus.Data.LLM;

/// <summary>
/// Google Gemini API implementation of <see cref="ILLMService"/>.
/// Uses the generateContent REST endpoint of the Gemini generative language API.
/// Conforms to the same ILLMService contract as LocalLLMService so the entire
/// agent stack (IntentParser, Orchestrator, tools) requires no changes.
/// </summary>
public class GeminiLLMService : ILLMService
{
    private const string GeminiBaseUrl = "https://generativelanguage.googleapis.com";

    private readonly HttpClient _httpClient;
    private readonly LLMOptions _options;
    private readonly ILogger<GeminiLLMService> _logger;

    public GeminiLLMService(
        HttpClient httpClient,
        IOptions<LLMOptions> options,
        ILogger<GeminiLLMService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(GeminiBaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // -------------------------------------------------------------------------
    // GenerateCompletionAsync
    // -------------------------------------------------------------------------
    public async Task<LLMResponse> GenerateCompletionAsync(
        string prompt,
        string? systemPrompt = null,
        bool requestJson = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return LLMResponse.Failure("Prompt cannot be empty.", _options.Model);

        var sw = Stopwatch.StartNew();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(
            Math.Max(10, Math.Min(_options.TimeoutSeconds, 60))));

        try
        {
            var apiKey = _options.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return LLMResponse.Failure(
                    "Gemini API key is not configured. Set LLM:ApiKey in appsettings.json.",
                    _options.Model);
            }

            var model = string.IsNullOrWhiteSpace(_options.Model)
                ? "gemini-3.5-flash"
                : _options.Model;

            var candidateModels = new List<string> { model, "gemini-3.5-flash", "gemini-3.1-flash-lite", "gemini-3.6-flash" };

            var fullPrompt = BuildFullPrompt(prompt, systemPrompt, requestJson);

            HttpResponseMessage? response = null;
            string responseBody = string.Empty;
            string usedModel = model;

            foreach (var currentModel in candidateModels.Distinct())
            {
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
                    generationConfig = new
                    {
                        temperature = 0.2,
                        topK = 40,
                        topP = 0.95,
                        maxOutputTokens = 2048,
                        responseMimeType = requestJson ? "application/json" : "text/plain"
                    }
                };

                var bodyJson = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

                var endpoint = $"/v1beta/models/{currentModel}:generateContent?key={apiKey}";
                response = await _httpClient.PostAsync(endpoint, content, cts.Token);
                responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                usedModel = currentModel;

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                _logger.LogWarning("Gemini API call to model '{Model}' returned HTTP {StatusCode}. Trying fallback model if available...", currentModel, (int)response.StatusCode);

                if ((int)response.StatusCode != 404 && (int)response.StatusCode != 503 && (int)response.StatusCode != 429)
                {
                    break; // Non-retryable error (e.g. 401 Unauthorized) — don't retry other models
                }
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                sw.Stop();
                _logger.LogWarning("Gemini API returned HTTP {StatusCode}: {Body}", (int)(response?.StatusCode ?? 0), responseBody);
                var errorMsg = TryExtractGeminiError(responseBody) ?? $"Gemini API error HTTP {(int)(response?.StatusCode ?? 0)}.";
                return LLMResponse.Failure(errorMsg, usedModel);
            }

            // Parse Gemini response structure
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string completionText = string.Empty;
            if (root.TryGetProperty("candidates", out var candidates)
                && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var contentProp)
                    && contentProp.TryGetProperty("parts", out var parts)
                    && parts.GetArrayLength() > 0)
                {
                    completionText = parts[0].TryGetProperty("text", out var textProp)
                        ? textProp.GetString() ?? string.Empty
                        : string.Empty;
                }
            }

            sw.Stop();

            // Token usage (optional fields Gemini may or may not return)
            int? promptTokens = null;
            int? completionTokens = null;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount", out var ptc))
                    promptTokens = ptc.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var ctc))
                    completionTokens = ctc.GetInt32();
            }

            _logger.LogDebug(
                "Gemini generated {Chars} chars in {Ms}ms (promptTokens={PT}, completionTokens={CT})",
                completionText.Length, sw.ElapsedMilliseconds, promptTokens, completionTokens);

            return LLMResponse.Success(completionText, model, sw.ElapsedMilliseconds,
                promptTokens, completionTokens);
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            _logger.LogWarning("Gemini API call timed out after {Timeout}s.", _options.TimeoutSeconds);
            return LLMResponse.Failure(
                $"Gemini API request timed out after {_options.TimeoutSeconds}s.",
                _options.Model);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "HTTP error calling Gemini API.");
            return LLMResponse.Failure($"Gemini HTTP error: {ex.Message}", _options.Model);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Unexpected error calling Gemini API.");
            return LLMResponse.Failure($"Unexpected Gemini error: {ex.Message}", _options.Model);
        }
    }

    // -------------------------------------------------------------------------
    // GenerateJsonAsync<T>
    // -------------------------------------------------------------------------
    public async Task<T?> GenerateJsonAsync<T>(
        string prompt,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        // Append strict JSON instruction to the system prompt
        var jsonSystemPrompt =
            (systemPrompt ?? string.Empty)
            + "\n\nCRITICAL: Respond with ONLY a raw valid JSON object. "
            + "Do NOT include any markdown code fences (```), do NOT include any "
            + "explanatory text before or after the JSON. "
            + "Your entire response must be parseable as JSON.";

        var response = await GenerateCompletionAsync(
            prompt, jsonSystemPrompt, requestJson: true, cancellationToken);

        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Content))
        {
            _logger.LogWarning(
                "Gemini returned no usable content for JSON request. Error: {Error}",
                response.ErrorMessage);
            return default;
        }

        try
        {
            var sanitized = SanitizeJsonOutput(response.Content);
            return JsonSerializer.Deserialize<T>(sanitized,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to deserialize Gemini JSON response. Raw content: {Content}",
                response.Content);
            return default;
        }
    }

    // -------------------------------------------------------------------------
    // CheckHealthAsync
    // -------------------------------------------------------------------------
    public async Task<LLMHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var model = string.IsNullOrWhiteSpace(_options.Model)
            ? "gemini-3.6-flash"
            : _options.Model;

        var apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            sw.Stop();
            return new LLMHealthStatus
            {
                IsAvailable = false,
                Provider = "Gemini",
                Model = model,
                BaseUrl = GeminiBaseUrl,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ErrorMessage = "Gemini API key is not configured (LLM:ApiKey)."
            };
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            // Lightweight health-check call — GET model metadata without consuming token generation quota
            var endpoint = $"/v1beta/models/{model}?key={apiKey}";
            var response = await _httpClient.GetAsync(endpoint, cts.Token);

            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new LLMHealthStatus
                {
                    IsAvailable = true,
                    Provider = "Gemini",
                    Model = model,
                    BaseUrl = GeminiBaseUrl,
                    ResponseTimeMs = sw.ElapsedMilliseconds
                };
            }

            // If 429 Too Many Requests, the API key and model are valid, but rate limited. Treat as available.
            if ((int)response.StatusCode == 429)
            {
                return new LLMHealthStatus
                {
                    IsAvailable = true,
                    Provider = "Gemini",
                    Model = model,
                    BaseUrl = GeminiBaseUrl,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    ErrorMessage = "Gemini API is rate limited (429 Too Many Requests)."
                };
            }

            // Fallback: Check general models list endpoint if specific model returns 404
            var listEndpoint = $"/v1beta/models?key={apiKey}";
            var listResponse = await _httpClient.GetAsync(listEndpoint, cts.Token);
            if (listResponse.IsSuccessStatusCode || (int)listResponse.StatusCode == 429)
            {
                return new LLMHealthStatus
                {
                    IsAvailable = true,
                    Provider = "Gemini",
                    Model = model,
                    BaseUrl = GeminiBaseUrl,
                    ResponseTimeMs = sw.ElapsedMilliseconds
                };
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            return new LLMHealthStatus
            {
                IsAvailable = false,
                Provider = "Gemini",
                Model = model,
                BaseUrl = GeminiBaseUrl,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ErrorMessage = $"Gemini health check failed HTTP {(int)response.StatusCode}: {body[..Math.Min(body.Length, 200)]}"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new LLMHealthStatus
            {
                IsAvailable = false,
                Provider = "Gemini",
                Model = model,
                BaseUrl = GeminiBaseUrl,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ErrorMessage = $"Gemini health check exception: {ex.Message}"
            };
        }
    }

    // -------------------------------------------------------------------------
    // Private Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Combines systemPrompt and user prompt into a single text block for Gemini,
    /// which uses a simple contents/parts structure rather than a messages array.
    /// </summary>
    private static string BuildFullPrompt(
        string userPrompt,
        string? systemPrompt,
        bool requestJson)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            sb.AppendLine("=== SYSTEM INSTRUCTIONS ===");
            sb.AppendLine(systemPrompt.Trim());
            sb.AppendLine();
        }

        if (requestJson)
        {
            sb.AppendLine("IMPORTANT: Your response must be valid JSON ONLY. No markdown fences, no extra text.");
            sb.AppendLine();
        }

        sb.AppendLine("=== USER REQUEST ===");
        sb.Append(userPrompt.Trim());

        return sb.ToString();
    }

    /// <summary>
    /// Strip markdown code fences and extract the innermost JSON object/array.
    /// Matches LocalLLMService.SanitizeJsonOutput behaviour.
    /// </summary>
    private static string SanitizeJsonOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";

        var trimmed = text.Trim();

        // Remove ```json ... ``` or ``` ... ```
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[7..];
        else if (trimmed.StartsWith("```"))
            trimmed = trimmed[3..];

        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3];

        trimmed = trimmed.Trim();

        // Extract the JSON object/array
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
            return trimmed[firstBrace..(lastBrace + 1)];

        // Try array form
        var firstBracket = trimmed.IndexOf('[');
        var lastBracket = trimmed.LastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket)
            return trimmed[firstBracket..(lastBracket + 1)];

        return trimmed;
    }

    /// <summary>
    /// Attempt to extract a human-readable error message from Gemini's error JSON.
    /// </summary>
    private static string? TryExtractGeminiError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch
        {
            // ignore parse errors — caller will use generic message
        }
        return null;
    }
}
