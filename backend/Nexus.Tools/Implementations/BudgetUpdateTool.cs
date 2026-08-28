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

    /// <summary>Reads a decimal from ArgumentsJson regardless of whether the JSON token is a number or a quoted string.</summary>
    private static decimal GetDecimalArgument(ToolExecutionContext context, params string[] keys)
    {
        foreach (var key in keys)
        {
            // Try direct decimal deserialization first (JSON number)
            var direct = context.GetArgument<decimal?>(key);
            if (direct is { } d && d > 0) return d;

            // Fallback: read as string and parse (JSON string like "20000" or "500k")
            var raw = context.GetArgument<string>(key);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                raw = raw.Trim();
                if (raw.EndsWith("k", StringComparison.OrdinalIgnoreCase) &&
                    decimal.TryParse(raw[..^1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var kv))
                    return kv * 1000m;
                if (raw.EndsWith("m", StringComparison.OrdinalIgnoreCase) &&
                    decimal.TryParse(raw[..^1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var mv))
                    return mv * 1_000_000m;
                if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sv) && sv > 0)
                    return sv;
            }
        }
        return 0m;
    }

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var dept = context.GetArgument<string>("department");
        var amt = GetDecimalArgument(context, "amount", "budgetAmount");
        var pct = GetDecimalArgument(context, "percentage");
        var prompt = context.GetArgument<string>("prompt")?.ToLowerInvariant() ?? string.Empty;
        var isMasterPool = context.GetArgument<bool?>("isMasterPool") ?? (prompt.Contains("overall") || prompt.Contains("master") || prompt.Contains("company budget") || prompt.Contains("total budget") || prompt.Contains("corporate budget") || prompt.Contains("1 billion") || prompt.Contains("1b"));

        if (!isMasterPool && string.IsNullOrWhiteSpace(dept))
            return Task.FromResult(ValidationResult.Failure("Department name is required."));
        if (amt <= 0 && pct <= 0 && !isMasterPool)
            return Task.FromResult(ValidationResult.Failure("A valid budget amount or percentage is required."));
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
            // Use the robust helper that handles both JSON number (20000) and JSON string ("20000" / "20k")
            var amount = GetDecimalArgument(context, "amount", "budgetAmount");
            var pct = GetDecimalArgument(context, "percentage");
            var quarter = context.GetArgument<string>("quarter") ?? "Q3";
            var year = context.GetArgument<int?>("year") ?? DateTime.UtcNow.Year;
            var prompt = context.GetArgument<string>("prompt")?.ToLowerInvariant() ?? string.Empty;
            
            var modeArg = context.GetArgument<string>("mode")?.ToUpperInvariant();
            string mode;
            if (!string.IsNullOrWhiteSpace(modeArg))
            {
                mode = modeArg;
            }
            else if (prompt.Contains("increase") || prompt.Contains("add") || prompt.Contains("plus") || prompt.Contains("more") || prompt.Contains("give"))
            {
                mode = "ADD";
            }
            else if (prompt.Contains("decrease") || prompt.Contains("reduce") || prompt.Contains("cut") || prompt.Contains("subtract") || prompt.Contains("minus") || prompt.Contains("less"))
            {
                mode = "SUBTRACT";
            }
            else
            {
                mode = "SET";
            }

            var isMasterPool = context.GetArgument<bool?>("isMasterPool") ?? (prompt.Contains("overall") || prompt.Contains("master") || prompt.Contains("company budget") || prompt.Contains("total budget") || prompt.Contains("corporate budget") || prompt.Contains("1 billion") || prompt.Contains("1b"));

            // 1. MASTER CORPORATE BUDGET POOL UPDATE
            if (isMasterPool)
            {
                try
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MasterBudgets') BEGIN CREATE TABLE MasterBudgets (Id INT IDENTITY(1,1) PRIMARY KEY, Year INT NOT NULL DEFAULT 2026, FiscalYear NVARCHAR(50) NOT NULL DEFAULT '2026-2027', TotalBudgetPool DECIMAL(18,2) NOT NULL DEFAULT 1000000000.00, UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()) END");
                }
                catch { }

                var master = await _db.MasterBudgets.FirstOrDefaultAsync(m => m.Year == year);
                if (master == null)
                {
                    master = new Nexus.Data.Entities.MasterBudget { Year = year, TotalBudgetPool = amount > 0 ? amount : 1_000_000_000m };
                    _db.MasterBudgets.Add(master);
                }
                else if (amount > 0)
                {
                    master.TotalBudgetPool = amount;
                    master.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();

                var totalDeptAllocated = await _db.Budgets.SumAsync(b => b.AllocatedAmount);
                var remainingPool = master.TotalBudgetPool - totalDeptAllocated;

                sw.Stop();
                return ToolExecutionResult.Success(new
                {
                    message = $"Master corporate budget pool for fiscal year {year}-2027 set to ${master.TotalBudgetPool:N2}. Total allocated across departments: ${totalDeptAllocated:N2}. Remaining unallocated corporate pool: ${remainingPool:N2}.",
                    totalMasterBudgetPool = master.TotalBudgetPool,
                    totalDepartmentAllocated = totalDeptAllocated,
                    remainingUnallocatedPool = remainingPool,
                    fiscalYear = master.FiscalYear
                }, Definition.RiskLevel, sw.ElapsedMilliseconds);
            }

            // 2. DEPARTMENT BUDGET ALLOCATION (with Auto-Creation if Department is new)
            var dept = await _db.Departments
                .FirstOrDefaultAsync(d => d.Name.ToLower().Equals(deptName.ToLower()) || d.Name.ToLower().Contains(deptName.ToLower()) || deptName.ToLower().Contains(d.Name.ToLower()));
            
            if (dept == null)
            {
                var cleanDeptName = deptName.Trim();
                if (cleanDeptName.Equals("IT", StringComparison.OrdinalIgnoreCase) || cleanDeptName.Equals("HR", StringComparison.OrdinalIgnoreCase) || cleanDeptName.Equals("QA", StringComparison.OrdinalIgnoreCase) || cleanDeptName.Equals("SQA", StringComparison.OrdinalIgnoreCase) || cleanDeptName.Equals("R&D", StringComparison.OrdinalIgnoreCase))
                {
                    cleanDeptName = cleanDeptName.ToUpperInvariant();
                }
                else
                {
                    cleanDeptName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanDeptName.ToLower());
                }

                dept = new Nexus.Data.Entities.Department
                {
                    Name = cleanDeptName,
                    Description = $"{cleanDeptName} Department"
                };
                _db.Departments.Add(dept);
                await _db.SaveChangesAsync();
            }

            // Find or create budget record (match by DepartmentId to prevent duplicate budget records)
            var budget = await _db.Budgets
                .FirstOrDefaultAsync(b => b.DepartmentId == dept.Id);

            decimal oldAmount = budget?.AllocatedAmount ?? 0m;

            // Handle percentage calculation if explicit amount was not provided
            if (amount <= 0 && pct > 0)
            {
                var deptSalarySum = await _db.Employees.Where(e => e.DepartmentId == dept.Id).SumAsync(e => (decimal?)e.Salary) ?? 0m;
                var baseAmount = oldAmount > 0 ? oldAmount : (deptSalarySum > 0 ? deptSalarySum : 0m);
                amount = Math.Round(baseAmount * (pct / 100m), 2);
            }

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

            // Sync raw SQL DepartmentBudgets table if present in SQL Server
            try
            {
                await _db.Database.ExecuteSqlRawAsync(
                    @"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DepartmentBudgets')
                      BEGIN
                          IF EXISTS (SELECT 1 FROM [dbo].[DepartmentBudgets] WHERE LOWER(DepartmentName) = LOWER({1}) OR LOWER(DepartmentName) LIKE {2})
                              UPDATE [dbo].[DepartmentBudgets] SET AllocatedAmount = {0} WHERE LOWER(DepartmentName) = LOWER({1}) OR LOWER(DepartmentName) LIKE {2}
                          ELSE
                              INSERT INTO [dbo].[DepartmentBudgets] (DepartmentName, Year, Quarter, AllocatedAmount, SpentAmount) VALUES ({1}, {3}, {4}, {0}, 0.00)
                      END",
                    newAmount, dept.Name, "%" + dept.Name.ToLower() + "%", year, quarter);
            }
            catch { }

            // Explicit Post-Execution SQL Verification Check:
            // Verify that SQL Server actually holds the requested allocation for the target Department
            var verifiedBudget = await _db.Budgets
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.DepartmentId == dept.Id);

            if (verifiedBudget == null || Math.Abs(verifiedBudget.AllocatedAmount - newAmount) > 0.01m)
            {
                return ToolExecutionResult.Failure(
                    $"SQL Server Verification Failed: Expected budget ${newAmount:N2} for department '{dept.Name}' (ID: {dept.Id}), but database holds ${verifiedBudget?.AllocatedAmount ?? 0m:N2}.",
                    Definition.RiskLevel);
            }

            // Track Master Pool remaining balance
            var masterPool = await _db.MasterBudgets.FirstOrDefaultAsync(m => m.Year == year);
            decimal poolTotal = masterPool?.TotalBudgetPool ?? 1_000_000_000m;
            decimal totalAllocatedAllDepts = await _db.Budgets.SumAsync(b => b.AllocatedAmount);
            decimal remainingPoolAfterAlloc = poolTotal - totalAllocatedAllDepts;

            sw.Stop();

            var changeDesc = mode switch
            {
                "SET" => $"set to ${newAmount:N2}",
                "SUBTRACT" => $"reduced by ${amount:N2} (was ${oldAmount:N2}, now ${newAmount:N2})",
                _ => $"increased by ${amount:N2} (was ${oldAmount:N2}, now ${newAmount:N2})"
            };

            return ToolExecutionResult.Success(new
            {
                message = $"{dept.Name} {quarter} {year} budget {changeDesc}. Total allocated across departments: ${totalAllocatedAllDepts:N2}. Remaining unallocated corporate pool: ${remainingPoolAfterAlloc:N2}.",
                departmentId = dept.Id,
                departmentName = dept.Name,
                quarter,
                year,
                oldAllocatedAmount = oldAmount,
                newAllocatedAmount = newAmount,
                change = amount,
                totalMasterPool = poolTotal,
                totalAllocatedAllDepts = totalAllocatedAllDepts,
                remainingUnallocatedPool = remainingPoolAfterAlloc,
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
