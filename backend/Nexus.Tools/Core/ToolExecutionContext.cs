using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Nexus.Tools.Core;

public class ToolExecutionContext
{
    public string ArgumentsJson { get; set; } = "{}";
    public string UserRole { get; set; } = "User";
    public int? UserId { get; set; }
    public HashSet<string> UserPermissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Guid? AgentRunId { get; set; }
    public Guid? ActionId { get; set; }

    public T? GetArgument<T>(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(ArgumentsJson)) return default;

        try
        {
            using var doc = JsonDocument.Parse(ArgumentsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return default;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return JsonSerializer.Deserialize<T>(prop.Value.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }
        }
        catch
        {
            return default;
        }

        return default;
    }
}
