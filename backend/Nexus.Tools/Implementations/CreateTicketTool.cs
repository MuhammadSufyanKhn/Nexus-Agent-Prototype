using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexus.Data.Enums;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class CreateTicketTool : IAgentTool
{
    private readonly IPythonAutomationService _pythonService;
    private readonly ILogger<CreateTicketTool> _logger;

    public CreateTicketTool(IPythonAutomationService pythonService, ILogger<CreateTicketTool> logger)
    {
        _pythonService = pythonService;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "ticket.create",
        Description = "Creates automated IT provisioning tickets for employee hardware/software onboarding via Python automation subsystem.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""employee"": { ""type"": ""string"" },
            ""department"": { ""type"": ""string"" },
            ""requestType"": { ""type"": ""string"" }
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

        var jsonResultStr = await _pythonService.ExecuteOperationAsync("ticket.create", jsonArgs);

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
