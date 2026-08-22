using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nexus.Data.Services;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class ExpenseReadTool : IAgentTool
{
    private readonly IExpenseService _expenseService;

    public ExpenseReadTool(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public ToolDefinition Definition => new()
    {
        Name = "expense.read",
        Description = "Retrieves expense claims filtered by employee ID or compliance status.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""id"": { ""type"": ""integer"" },
            ""employeeId"": { ""type"": ""integer"" },
            ""status"": { ""type"": ""integer"" }
          }
        }",
        RequiredPermission = "expense.read",
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
                var expense = await _expenseService.GetByIdAsync(id.Value);
                sw.Stop();
                if (expense == null) return ToolExecutionResult.Failure($"Expense claim with ID {id.Value} not found.", Definition.RiskLevel);
                return ToolExecutionResult.Success(expense, Definition.RiskLevel, sw.ElapsedMilliseconds);
            }

            var employeeId = context.GetArgument<int?>("employeeId");
            var statusInt = context.GetArgument<int?>("status");
            ExpenseStatus? status = statusInt.HasValue ? (ExpenseStatus)statusInt.Value : null;

            var expenses = await _expenseService.GetAllAsync(employeeId, status);
            sw.Stop();
            return ToolExecutionResult.Success(expenses, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
