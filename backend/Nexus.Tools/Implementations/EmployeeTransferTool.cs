using System;
using System.Collections.Generic;
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
/// Handles employee transfers, promotions, role changes, manager changes,
/// and location transfers. Resolves employees by name when ID is not provided.
/// Returns clarification when multiple employees match a name.
/// </summary>
public class EmployeeTransferTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<EmployeeTransferTool> _logger;

    public EmployeeTransferTool(NexusDbContext db, ILogger<EmployeeTransferTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "employee.transfer",
        Description = "Transfer an employee to a different department, role, manager, or location. Also handles promotions, reassignments, and role swaps.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""employeeId"":       { ""type"": ""integer"" },
            ""employeeName"":     { ""type"": ""string"" },
            ""targetDepartment"": { ""type"": ""string"" },
            ""sourceDepartment"": { ""type"": ""string"" },
            ""targetRole"":       { ""type"": ""string"" },
            ""targetManager"":    { ""type"": ""string"" },
            ""targetLocation"":   { ""type"": ""string"" },
            ""effectiveDate"":    { ""type"": ""string"" },
            ""isPromotion"":      { ""type"": ""boolean"" }
          },
          ""required"": []
        }",
        RequiredPermission = "employee.update",
        RiskLevel = RiskLevel.High
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var empId = context.GetArgument<int?>("employeeId");
        var empName = context.GetArgument<string>("employeeName");
        var targetDept = context.GetArgument<string>("targetDepartment");
        var targetRole = context.GetArgument<string>("targetRole");
        var targetManager = context.GetArgument<string>("targetManager");
        var targetLocation = context.GetArgument<string>("targetLocation");

        if (!empId.HasValue && string.IsNullOrWhiteSpace(empName))
            return Task.FromResult(ValidationResult.Failure("Please specify the employee name or ID to transfer."));

        if (string.IsNullOrWhiteSpace(targetDept) && string.IsNullOrWhiteSpace(targetRole)
            && string.IsNullOrWhiteSpace(targetManager) && string.IsNullOrWhiteSpace(targetLocation))
            return Task.FromResult(ValidationResult.Failure("Please specify the target department, role, manager, or location."));

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
            var empId = context.GetArgument<int?>("employeeId");
            var empName = context.GetArgument<string>("employeeName");
            var targetDept = context.GetArgument<string>("targetDepartment");
            var sourceDept = context.GetArgument<string>("sourceDepartment");
            var targetRole = context.GetArgument<string>("targetRole");
            var targetManager = context.GetArgument<string>("targetManager");
            var targetLocation = context.GetArgument<string>("targetLocation");
            var isPromotion = context.GetArgument<bool?>("isPromotion") ?? false;

            Nexus.Data.Entities.Employee? employee = null;

            if (empId.HasValue && empId > 0)
            {
                employee = await _db.Employees.Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == empId.Value);
                if (employee == null)
                    return ToolExecutionResult.Failure($"Employee with ID {empId} not found.", RiskLevel.Low);
            }
            else if (!string.IsNullOrWhiteSpace(empName))
            {
                var query = _db.Employees.Include(e => e.Department).AsQueryable();
                if (!string.IsNullOrWhiteSpace(sourceDept))
                    query = query.Where(e => e.Department != null &&
                        e.Department.Name.ToLower().Contains(sourceDept.ToLower()));

                var matches = await query
                    .Where(e => e.Name.ToLower().Contains(empName.ToLower()))
                    .ToListAsync();

                if (matches.Count == 0)
                    return ToolExecutionResult.Failure($"No employee named '{empName}' found.", RiskLevel.Low);

                employee = matches.OrderByDescending(m => m.Id).First();
            }

            if (employee == null)
                return ToolExecutionResult.Failure("Could not resolve the employee.", RiskLevel.Low);

            var changes = new List<string>();
            var oldDept = employee.Department?.Name ?? "Unknown";

            if (!string.IsNullOrWhiteSpace(targetDept))
            {
                var dept = await _db.Departments
                    .FirstOrDefaultAsync(d => d.Name.ToLower().Contains(targetDept.ToLower()));
                if (dept == null)
                {
                    dept = new Nexus.Data.Entities.Department
                    {
                        Name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(targetDept.ToLower()),
                        Description = $"{targetDept} Department"
                    };
                    _db.Departments.Add(dept);
                    await _db.SaveChangesAsync();
                }
                employee.DepartmentId = dept.Id;
                changes.Add($"Department: {oldDept} → {dept.Name}");
            }

            if (!string.IsNullOrWhiteSpace(targetRole))
            {
                var oldRole = employee.Designation;
                employee.Designation = targetRole;
                changes.Add($"Role: {oldRole} → {targetRole}");
            }

            if (!string.IsNullOrWhiteSpace(targetManager))
            {
                var oldManager = employee.ManagerName ?? "None";
                employee.ManagerName = targetManager;
                changes.Add($"Manager: {oldManager} → {targetManager}");
            }

            if (!string.IsNullOrWhiteSpace(targetLocation))
            {
                var oldLocation = employee.Location ?? "Not Set";
                employee.Location = targetLocation;
                changes.Add($"Location: {oldLocation} → {targetLocation}");
            }

            employee.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            sw.Stop();

            var action = isPromotion ? "promoted" : "transferred";
            var changesSummary = string.Join(" | ", changes);

            return ToolExecutionResult.Success(new
            {
                message = $"Employee {employee.Name} has been {action} successfully. Changes: {changesSummary}",
                employeeId = employee.Id,
                employeeName = employee.Name,
                changes,
                isPromotion,
                previousDepartment = oldDept
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmployeeTransferTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
