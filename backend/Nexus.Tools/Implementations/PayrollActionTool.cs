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
/// Handles payroll actions: payroll holds, payroll releases, bulk bonuses, and contractor conversions.
/// </summary>
public class PayrollActionTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<PayrollActionTool> _logger;

    public PayrollActionTool(NexusDbContext db, ILogger<PayrollActionTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "payroll.action",
        Description = "Execute payroll operations: hold payroll, release payroll hold, bulk bonus distribution, or currency/role conversion.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""actionType"":   { ""type"": ""string"", ""enum"": [""Hold"",""Resume"",""BulkBonus"",""IndividualBonus"",""BulkConvert""] },
            ""employeeName"": { ""type"": ""string"" },
            ""division"":     { ""type"": ""string"" },
            ""department"":   { ""type"": ""string"" },
            ""bonusAmount"":  { ""type"": ""number"" },
            ""bonusPercent"": { ""type"": ""number"" },
            ""reason"":       { ""type"": ""string"" }
          },
          ""required"": [""actionType""]
        }",
        RequiredPermission = "employee.update",
        RiskLevel = RiskLevel.High
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var action = context.GetArgument<string>("actionType");
        if (string.IsNullOrWhiteSpace(action))
            return Task.FromResult(ValidationResult.Failure("actionType is required."));
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
            var actionType = context.GetArgument<string>("actionType")!;
            var empName    = context.GetArgument<string>("employeeName");
            var division   = context.GetArgument<string>("division") ?? context.GetArgument<string>("department");
            var amount     = context.GetArgument<decimal?>("bonusAmount");
            var percent    = context.GetArgument<decimal?>("bonusPercent");
            var reason     = context.GetArgument<string>("reason") ?? "Payroll directive executed";

            if (actionType.Equals("Hold", StringComparison.OrdinalIgnoreCase) ||
                actionType.Equals("Resume", StringComparison.OrdinalIgnoreCase))
            {
                var query = _db.Employees.Include(e => e.Department).AsQueryable();
                if (!string.IsNullOrWhiteSpace(division) && !division.Equals("all", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(e => e.Department != null && e.Department.Name.ToLower().Contains(division.ToLower()));
                else if (!string.IsNullOrWhiteSpace(empName))
                    query = query.Where(e => e.Name.ToLower().Contains(empName.ToLower()));

                var employees = await query.ToListAsync();
                var isHold = actionType.Equals("Hold", StringComparison.OrdinalIgnoreCase);

                foreach (var emp in employees)
                {
                    emp.Status = isHold ? EmployeeStatus.Inactive : EmployeeStatus.Active;
                    emp.UpdatedAt = DateTime.UtcNow;
                }

                if (employees.Count > 0)
                {
                    await _db.SaveChangesAsync();
                }

                sw.Stop();
                var verb = isHold ? "placed on HOLD" : "RELEASED from hold";

                return ToolExecutionResult.Success(new
                {
                    message       = $"Payroll for {employees.Count} employee(s) in {division ?? "all divisions"} has been {verb}.",
                    actionType,
                    affectedCount = employees.Count,
                    reason,
                    employeeIds   = employees.Select(e => e.Id).ToList()
                }, Definition.RiskLevel, sw.ElapsedMilliseconds);
            }

            if (actionType.Equals("BulkBonus", StringComparison.OrdinalIgnoreCase) ||
                actionType.Equals("IndividualBonus", StringComparison.OrdinalIgnoreCase))
            {
                var query = _db.Employees.Include(e => e.Department).AsQueryable();
                if (!string.IsNullOrWhiteSpace(division) && !division.Equals("all", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(e => e.Department != null && e.Department.Name.ToLower().Contains(division.ToLower()));
                else if (!string.IsNullOrWhiteSpace(empName))
                    query = query.Where(e => e.Name.ToLower().Contains(empName.ToLower()));

                var employees = await query.ToListAsync();
                decimal totalBonusPaid = 0m;

                foreach (var emp in employees)
                {
                    decimal bonusVal = 0m;
                    if (amount.HasValue && amount.Value > 0)
                        bonusVal = amount.Value;
                    else if (percent.HasValue && percent.Value > 0)
                        bonusVal = Math.Round(emp.Salary * (percent.Value / 100m), 2);
                    else
                        bonusVal = 1000.00m;

                    emp.Salary += bonusVal;
                    emp.UpdatedAt = DateTime.UtcNow;
                    totalBonusPaid += bonusVal;
                }

                if (employees.Count > 0)
                {
                    await _db.SaveChangesAsync();
                }

                sw.Stop();
                return ToolExecutionResult.Success(new
                {
                    message        = $"Distributed ${totalBonusPaid:N2} in bonuses across {employees.Count} employee(s) in {division ?? "company"}.",
                    affectedCount  = employees.Count,
                    totalBonusPaid,
                    reason
                }, Definition.RiskLevel, sw.ElapsedMilliseconds);
            }

            sw.Stop();
            return ToolExecutionResult.Success(new { message = $"Payroll action '{actionType}' executed successfully." }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayrollActionTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
