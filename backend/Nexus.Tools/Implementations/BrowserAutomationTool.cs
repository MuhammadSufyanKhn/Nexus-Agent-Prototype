using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexus.Data.Enums;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class BrowserAutomationTool : IAgentTool
{
    private readonly IPythonAutomationService _pythonService;
    private readonly ILogger<BrowserAutomationTool> _logger;

    public BrowserAutomationTool(IPythonAutomationService pythonService, ILogger<BrowserAutomationTool> logger)
    {
        _pythonService = pythonService;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "onboarding.submit_legacy_form",
        Description = "Submits new employee profile into Legacy HR Portal via automated browser automation engine.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""employeeName"": { ""type"": ""string"" },
            ""department"": { ""type"": ""string"" },
            ""designation"": { ""type"": ""string"" },
            ""salary"": { ""type"": ""number"" },
            ""manager"": { ""type"": ""string"" }
          }
        }",
        RequiredPermission = "employee.create",
        RiskLevel = RiskLevel.Medium
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        var jsonArgs = string.IsNullOrWhiteSpace(context.ArgumentsJson) ? "{}" : context.ArgumentsJson;

        var jsonResultStr = await _pythonService.ExecuteOperationAsync("onboarding.submit_legacy_form", jsonArgs);

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
