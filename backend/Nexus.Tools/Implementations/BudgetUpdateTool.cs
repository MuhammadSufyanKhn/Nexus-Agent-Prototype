using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Updates the allocated budget for a department + optional quarter.
/// Supports semantic commands like "give IT 50k more", "set Finance Q3 budget to 200000",
/// "increase IT budget by 50000", "allocate additional 50k to Marketing".
/// </summary>
public class BudgetUpdateTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<BudgetUpdateTool> _logger;

    public BudgetUpdateTool(NexusDbContext db, ILogger<BudgetUpdateTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "budget.update",
        Description = "Increase, decrease, or set the allocated budget for a department. Supports natural language amounts like '50k', 'additional 50000', etc.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""department"":   { ""type"": ""string"" },
            ""budgetAmount"": { ""type"": ""number"" },
            ""quarter"":      { ""type"": ""string"" },
            ""year"":         { ""type"": ""integer"" },
            ""mode"":         { ""type"": ""string"", ""enum"": [""SET"",""ADD"",""SUBTRACT""] }
          },
          ""required"": [""department"", ""budgetAmount""]
        }",
        RequiredPermission = "budget.update",
        RiskLevel = RiskLevel.High
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var dept = context.GetArgument<string>("department");
        var amt = context.GetArgument<decimal?>("budgetAmount");
        if (string.IsNullOrWhiteSpace(dept))
            return Task.FromResult(ValidationResult.Failure("Department name is required."));
        if (amt is null || amt <= 0)
            return Task.FromResult(ValidationResult.Failure("A valid budget amount is required."));
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var validation = await ValidateInputAsync(context);
            if (!validation.IsValid)
                return ToolExecutionResult.Failure(string.Join("; ", validation.Errors), Definition.RiskLevel);

            var deptName = context.GetArgument<string>("department") ?? string.Empty;
            var amount = context.GetArgument<decimal?>("budgetAmount") ?? 0m;
            var quarter = context.GetArgument<string>("quarter") ?? "Q3";
            var year = context.GetArgument<int?>("year") ?? DateTime.UtcNow.Year;
            var mode = context.GetArgument<string>("mode")?.ToUpperInvariant() ?? "ADD";

            // Resolve department
            var dept = await _db.Departments
                .FirstOrDefaultAsync(d => d.Name.ToLower().Contains(deptName.ToLower()));
            if (dept == null)
                return ToolExecutionResult.Failure($"Department '{deptName}' not found.", RiskLevel.Low);

            // Find or create budget record
            var budget = await _db.Budgets
                .FirstOrDefaultAsync(b =>
                    b.DepartmentId == dept.Id &&
                    b.Quarter == quarter &&
                    b.Year == year);

            decimal oldAmount = 0m;
            decimal newAmount = 0m;

            if (budget == null)
            {
                // Create new budget record
                oldAmount = 0m;
                newAmount = mode == "SET" ? amount : amount;
                budget = new Nexus.Data.Entities.Budget
                {
                    DepartmentId = dept.Id,
                    Year = year,
                    Quarter = quarter,
                    AllocatedAmount = newAmount,
                    SpentAmount = 0m
                };
                _db.Budgets.Add(budget);
            }
            else
            {
                oldAmount = budget.AllocatedAmount;
                newAmount = mode switch
                {
                    "SET" => amount,
                    "SUBTRACT" => Math.Max(0m, oldAmount - amount),
                    _ => oldAmount + amount // ADD (default)
                };
                budget.AllocatedAmount = newAmount;
            }

            await _db.SaveChangesAsync();
            sw.Stop();

            var changeDesc = mode switch
            {
                "SET" => $"set to ${newAmount:N2}",
                "SUBTRACT" => $"reduced by ${amount:N2} (was ${oldAmount:N2}, now ${newAmount:N2})",
                _ => $"increased by ${amount:N2} (was ${oldAmount:N2}, now ${newAmount:N2})"
            };

            return ToolExecutionResult.Success(new
            {
                message = $"{dept.Name} {quarter} {year} budget {changeDesc}.",
                departmentId = dept.Id,
                departmentName = dept.Name,
                quarter,
                year,
                oldAllocatedAmount = oldAmount,
                newAllocatedAmount = newAmount,
                change = amount,
                mode
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BudgetUpdateTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
