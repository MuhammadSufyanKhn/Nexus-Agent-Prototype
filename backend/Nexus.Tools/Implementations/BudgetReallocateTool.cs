using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Reallocates budget atomically between two departments (transfer budget from source to target).
/// </summary>
public class BudgetReallocateTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<BudgetReallocateTool> _logger;

    public BudgetReallocateTool(NexusDbContext db, ILogger<BudgetReallocateTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "budget.reallocate",
        Description = "Reallocate/transfer allocated budget from one department to another.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""sourceDepartment"": { ""type"": ""string"" },
            ""targetDepartment"": { ""type"": ""string"" },
            ""amount"":           { ""type"": ""number"" },
            ""quarter"":          { ""type"": ""string"" },
            ""year"":             { ""type"": ""integer"" }
          },
          ""required"": [""sourceDepartment"", ""targetDepartment"", ""amount""]
        }",
        RequiredPermission = "budget.update",
        RiskLevel = RiskLevel.High
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var src  = context.GetArgument<string>("sourceDepartment");
        var tgt  = context.GetArgument<string>("targetDepartment");
        var amt  = context.GetArgument<decimal?>("amount");
        if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt))
            return Task.FromResult(ValidationResult.Failure("Both sourceDepartment and targetDepartment are required."));
        if (!amt.HasValue || amt.Value <= 0)
            return Task.FromResult(ValidationResult.Failure("A valid positive re-allocation amount is required."));
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var validation = await ValidateInputAsync(context);
        if (!validation.IsValid)
            return ToolExecutionResult.Failure(string.Join("; ", validation.Errors), Definition.RiskLevel);

        var sw = Stopwatch.StartNew();
        try
        {
            var srcName = context.GetArgument<string>("sourceDepartment")!;
            var tgtName = context.GetArgument<string>("targetDepartment")!;
            var amount  = context.GetArgument<decimal>("amount");
            var quarter = context.GetArgument<string>("quarter") ?? "Q3";
            var year    = context.GetArgument<int?>("year") ?? DateTime.UtcNow.Year;

            var srcDept = await _db.Departments.FirstOrDefaultAsync(d => d.Name.ToLower().Contains(srcName.ToLower()));
            var tgtDept = await _db.Departments.FirstOrDefaultAsync(d => d.Name.ToLower().Contains(tgtName.ToLower()));

            if (srcDept == null) return ToolExecutionResult.Failure($"Source department '{srcName}' not found.", RiskLevel.Low);
            if (tgtDept == null) return ToolExecutionResult.Failure($"Target department '{tgtName}' not found.", RiskLevel.Low);

            var srcBudget = await _db.Budgets.FirstOrDefaultAsync(b => b.DepartmentId == srcDept.Id);
            var tgtBudget = await _db.Budgets.FirstOrDefaultAsync(b => b.DepartmentId == tgtDept.Id);

            if (srcBudget == null)
                return ToolExecutionResult.Failure($"No {quarter} {year} budget record found for source department '{srcDept.Name}'.", RiskLevel.Low);

            if (tgtBudget == null)
            {
                tgtBudget = new Nexus.Data.Entities.Budget
                {
                    DepartmentId    = tgtDept.Id,
                    Year            = year,
                    Quarter         = quarter,
                    AllocatedAmount = 0m,
                    SpentAmount     = 0m
                };
                _db.Budgets.Add(tgtBudget);
            }

            srcBudget.AllocatedAmount = Math.Max(0m, srcBudget.AllocatedAmount - amount);
            tgtBudget.AllocatedAmount += amount;

            await _db.SaveChangesAsync();
            sw.Stop();

            return ToolExecutionResult.Success(new
            {
                message              = $"Successfully reallocated ${amount:N2} from {srcDept.Name} to {tgtDept.Name} ({quarter} {year}).",
                sourceDepartment     = srcDept.Name,
                targetDepartment     = tgtDept.Name,
                amountReallocated    = amount,
                sourceNewAllocated   = srcBudget.AllocatedAmount,
                targetNewAllocated   = tgtBudget.AllocatedAmount,
                quarter,
                year
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BudgetReallocateTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
