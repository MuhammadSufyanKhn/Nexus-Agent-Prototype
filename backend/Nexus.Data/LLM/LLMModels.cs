namespace Nexus.Data.LLM;

public class LLMOptions
{
    public const string SectionName = "LLM";

    /// <summary>
    /// For Gemini: "Gemini". For local Ollama: "Ollama".
    /// </summary>
    public string Provider { get; set; } = "Gemini";

    /// <summary>
    /// Gemini API key. Required when Provider is "Gemini".
    /// Store in appsettings.json or environment variables.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gemini model name (e.g. "gemini-2.0-flash") or Ollama model (e.g. "llama3.2").
    /// </summary>
    public string Model { get; set; } = "gemini-2.0-flash";

    /// <summary>
    /// Only used when Provider is "Ollama". Base URL of the local Ollama server.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Maximum seconds to wait for an LLM response before timing out.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

public class LLMResponse
{
    public bool IsSuccess { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }

    public static LLMResponse Success(string content, string model, long executionTimeMs, int? promptTokens = null, int? completionTokens = null)
    {
        return new LLMResponse
        {
            IsSuccess = true,
            Content = content,
            Model = model,
            ExecutionTimeMs = executionTimeMs,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens
        };
    }

    public static LLMResponse Failure(string errorMessage, string model = "")
    {
        return new LLMResponse
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Model = model
        };
    }
}

public class LLMHealthStatus
{
    public bool IsAvailable { get; set; }
    public string Provider { get; set; } = "Gemini";
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public long ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }

    // ── Frontend compatibility aliases ─────────────────────────────────────
    // The React api.ts type uses camelCase: modelName, responseLatencyMs, statusMessage
    public string ModelName => Model;
    public string ResponseLatencyMs => ResponseTimeMs.ToString();
    public string StatusMessage => IsAvailable
        ? $"{Provider} ({Model}) is online and responding."
        : ErrorMessage ?? $"{Provider} is unavailable.";
}
