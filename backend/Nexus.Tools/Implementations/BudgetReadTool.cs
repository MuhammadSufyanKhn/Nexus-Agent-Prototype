using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nexus.Data.Services;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class BudgetReadTool : IAgentTool
{
    private readonly IBudgetService _budgetService;

    public BudgetReadTool(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    public ToolDefinition Definition => new()
    {
        Name = "budget.read",
        Description = "Retrieves budget allocations, spent amounts, and over-budget variances by department, quarter, or year.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""id"": { ""type"": ""integer"" },
            ""departmentId"": { ""type"": ""integer"" },
            ""quarter"": { ""type"": ""string"" },
            ""year"": { ""type"": ""integer"" }
          }
        }",
        RequiredPermission = "budget.read",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var id = context.GetArgument<int?>("id");
            if (id.HasValue && id.Value > 0)
            {
                var budget = await _budgetService.GetByIdAsync(id.Value);
                sw.Stop();
                if (budget == null) return ToolExecutionResult.Failure($"Budget record with ID {id.Value} not found.", Definition.RiskLevel);
                return ToolExecutionResult.Success(budget, Definition.RiskLevel, sw.ElapsedMilliseconds);
            }

            var departmentId = context.GetArgument<int?>("departmentId");
            var quarter = context.GetArgument<string>("quarter");
            var year = context.GetArgument<int?>("year");

            var budgets = await _budgetService.GetAllAsync(departmentId, quarter, year);
            var minBudget = context.GetArgument<decimal?>("minBudget") ?? context.GetArgument<decimal?>("minAllocatedAmount") ?? context.GetArgument<decimal?>("minAmount");
            if (minBudget.HasValue && minBudget.Value > 0)
            {
                budgets = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(budgets, b => b.AllocatedAmount > minBudget.Value));
            }
            sw.Stop();
            return ToolExecutionResult.Success(budgets, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
