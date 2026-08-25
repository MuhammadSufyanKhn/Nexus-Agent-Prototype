using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Freezes or unfreezes department budget allocations (e.g. bulk freeze, department budget freeze).
/// </summary>
public class BudgetFreezeTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<BudgetFreezeTool> _logger;

    public BudgetFreezeTool(NexusDbContext db, ILogger<BudgetFreezeTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "budget.freeze",
        Description = "Freeze or unfreeze budget allocations for a specific department or all departments.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""department"": { ""type"": ""string"" },
            ""isFreeze"":   { ""type"": ""boolean"" },
            ""reason"":     { ""type"": ""string"" },
            ""scope"":      { ""type"": ""string"", ""enum"": [""SINGLE"",""ALL""] }
          },
          ""required"": []
        }",
        RequiredPermission = "budget.update",
        RiskLevel = RiskLevel.High
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
        => Task.FromResult(ValidationResult.Success());

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var deptName = context.GetArgument<string>("department");
            var isFreeze = context.GetArgument<bool?>("isFreeze") ?? true;
            var reason   = context.GetArgument<string>("reason") ?? (isFreeze ? "Executive budget freeze directive" : "Budget un-frozen");
            var scope    = context.GetArgument<string>("scope") ?? (string.Equals(deptName, "ALL", StringComparison.OrdinalIgnoreCase) ? "ALL" : "SINGLE");

            var query = _db.Budgets.Include(b => b.Department).AsQueryable();

            if (scope != "ALL" && !string.IsNullOrWhiteSpace(deptName))
            {
                query = query.Where(b => b.Department != null && b.Department.Name.ToLower().Contains(deptName.ToLower()));
            }

            var budgets = await query.ToListAsync();
            if (budgets.Count == 0)
                return ToolExecutionResult.Failure("No matching department budget records found.", RiskLevel.Low);

            foreach (var b in budgets)
            {
                b.IsFrozen     = isFreeze;
                b.FreezeReason = isFreeze ? reason : null;
                b.FrozenAt     = isFreeze ? DateTime.UtcNow : null;
            }

            await _db.SaveChangesAsync();
            sw.Stop();

            var stateWord = isFreeze ? "FROZEN" : "UNFROZEN";
            return ToolExecutionResult.Success(new
            {
                message       = $"Budget allocations for {budgets.Count} department(s) have been {stateWord}.",
                affectedCount = budgets.Count,
                isFrozen      = isFreeze,
                reason,
                departments   = budgets.Select(b => b.Department?.Name).ToList()
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BudgetFreezeTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
