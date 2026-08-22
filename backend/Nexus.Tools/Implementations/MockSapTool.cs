using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexus.Data.Enums;
using Nexus.Tools.Core;
using Nexus.Tools.Sap;

namespace Nexus.Tools.Implementations;

public class MockSapTool : IAgentTool
{
    private readonly ISapConnector _sapConnector;
    private readonly ILogger<MockSapTool> _logger;

    public MockSapTool(ISapConnector sapConnector, ILogger<MockSapTool> logger)
    {
        _sapConnector = sapConnector;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "sap.employee.create",
        Description = "Provisions new employee master data record in enterprise SAP HCM database (MOCK SAP CONNECTOR).",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""name"": { ""type"": ""string"" },
            ""department"": { ""type"": ""string"" },
            ""designation"": { ""type"": ""string"" },
            ""salary"": { ""type"": ""number"" }
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

        var name = context.GetArgument<string>("name") ?? "Ahmed Khan";
        var department = context.GetArgument<string>("department") ?? "IT";
        var designation = context.GetArgument<string>("designation") ?? "Mid-Level .NET Developer";
        var salary = context.GetArgument<decimal?>("salary") ?? 68000.00m;

        var result = await _sapConnector.CreateEmployeeMasterDataAsync(name, department, designation, salary);

        sw.Stop();
        return ToolExecutionResult.Success(result, Definition.RiskLevel, sw.ElapsedMilliseconds);
    }
}
