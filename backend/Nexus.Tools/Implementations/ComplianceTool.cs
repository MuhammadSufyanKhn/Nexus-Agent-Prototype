using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexus.Data.Policies;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class ComplianceTool : IAgentTool
{
    private readonly IPolicyComplianceEngine _complianceEngine;
    private readonly ILogger<ComplianceTool> _logger;

    public ComplianceTool(IPolicyComplianceEngine complianceEngine, ILogger<ComplianceTool> logger)
    {
        _complianceEngine = complianceEngine;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "compliance.check",
        Description = "Audits employee expense claims against company reimbursement policies and calculates compliance status, thresholds, and variances.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""employeeName"": { ""type"": ""string"" },
            ""employeeId"": { ""type"": ""number"" },
            ""expenseId"": { ""type"": ""number"" }
          }
        }",
        RequiredPermission = "compliance.check",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();

        var empName = context.GetArgument<string>("employeeName") ?? "Tariq Mahmood";
        var empId = context.GetArgument<int?>("employeeId");
        var expId = context.GetArgument<int?>("expenseId");

        var result = await _complianceEngine.EvaluateExpenseComplianceAsync(empName, empId, expId);

        sw.Stop();
        return ToolExecutionResult.Success(result, Definition.RiskLevel, sw.ElapsedMilliseconds);
    }
}
