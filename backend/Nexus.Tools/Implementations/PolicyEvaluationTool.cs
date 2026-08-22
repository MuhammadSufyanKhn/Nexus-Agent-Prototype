using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexus.Data.Policies;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class PolicyEvaluationTool : IAgentTool
{
    private readonly IPolicyRuleEngine _policyEngine;
    private readonly ILogger<PolicyEvaluationTool> _logger;

    public PolicyEvaluationTool(IPolicyRuleEngine policyEngine, ILogger<PolicyEvaluationTool> logger)
    {
        _policyEngine = policyEngine;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "policy.evaluate",
        Description = "Evaluates employee onboarding roles, compensation bands, or expense claims against company policy directives.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""role"": { ""type"": ""string"" },
            ""department"": { ""type"": ""string"" },
            ""experienceYears"": { ""type"": ""number"" },
            ""requestedSalary"": { ""type"": ""number"" },
            ""expenseType"": { ""type"": ""string"" },
            ""expenseAmount"": { ""type"": ""number"" }
          }
        }",
        RequiredPermission = "policy.evaluate",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();

        var evalContext = new PolicyEvaluationContext
        {
            Role = context.GetArgument<string>("role") ?? "Mid-Level .NET Developer",
            Department = context.GetArgument<string>("department") ?? "IT",
            ExperienceYears = context.GetArgument<int?>("experienceYears") ?? 3,
            RequestedSalary = context.GetArgument<decimal?>("requestedSalary"),
            ExpenseType = context.GetArgument<string>("expenseType"),
            ExpenseAmount = context.GetArgument<decimal?>("expenseAmount")
        };

        PolicyDecisionResult result;
        if (evalContext.ExpenseAmount.HasValue || !string.IsNullOrWhiteSpace(evalContext.ExpenseType))
        {
            result = await _policyEngine.EvaluateExpenseComplianceAsync(evalContext);
        }
        else
        {
            result = await _policyEngine.EvaluateOnboardingPolicyAsync(evalContext);
        }

        sw.Stop();
        return ToolExecutionResult.Success(result, Definition.RiskLevel, sw.ElapsedMilliseconds);
    }
}
