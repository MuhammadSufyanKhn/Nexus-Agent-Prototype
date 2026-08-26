using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexus.Data.Enums;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class WelcomeEmailTool : IAgentTool
{
    private readonly IPythonAutomationService _pythonService;
    private readonly ILogger<WelcomeEmailTool> _logger;

    public WelcomeEmailTool(IPythonAutomationService pythonService, ILogger<WelcomeEmailTool> logger)
    {
        _pythonService = pythonService;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "email.welcome",
        Description = "Generates official onboarding welcome email for new employees via Python email automation subsystem.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""name"": { ""type"": ""string"" },
            ""email"": { ""type"": ""string"" },
            ""recipient_email"": { ""type"": ""string"" },
            ""designation"": { ""type"": ""string"" },
            ""department"": { ""type"": ""string"" }
          }
        }",
        RequiredPermission = "employee.read",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        var jsonArgs = string.IsNullOrWhiteSpace(context.ArgumentsJson) ? "{}" : context.ArgumentsJson;

        var jsonResultStr = await _pythonService.ExecuteOperationAsync("email.welcome", jsonArgs);

        sw.Stop();
        try
        {
            using var doc = JsonDocument.Parse(jsonResultStr);
            return ToolExecutionResult.Success(doc.RootElement.Clone(), Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch
        {
            return ToolExecutionResult.Success(new { result = jsonResultStr }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
    }
}
