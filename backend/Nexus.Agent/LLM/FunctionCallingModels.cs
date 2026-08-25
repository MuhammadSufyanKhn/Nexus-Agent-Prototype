using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nexus.Agent.LLM;

public class FunctionCallResponse
{
    public bool HasFunctionCall { get; set; }
    public string? FunctionName { get; set; }
    public Dictionary<string, object> Arguments { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
    public string? TextResponse { get; set; }
    public string Model { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public class GeminiFunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public object Parameters { get; set; } = new { type = "object", properties = new { } };
}
