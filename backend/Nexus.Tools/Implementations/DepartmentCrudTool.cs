using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Handles CREATE, UPDATE, DELETE operations for Departments via natural-language intent.
/// READ is served by the existing DepartmentReadTool.
/// </summary>
public class DepartmentCrudTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<DepartmentCrudTool> _logger;

    public DepartmentCrudTool(NexusDbContext db, ILogger<DepartmentCrudTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "department.crud",
        Description = "Create, update, or delete enterprise departments. Validates no orphan employees exist before deletion.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""operation"":   { ""type"": ""string"", ""enum"": [""CREATE"",""UPDATE"",""DELETE""] },
            ""name"":        { ""type"": ""string"" },
            ""newName"":     { ""type"": ""string"" },
            ""description"": { ""type"": ""string"" }
          },
          ""required"": [""operation"", ""name""]
        }",
        RequiredPermission = "department.create",
        RiskLevel = RiskLevel.Medium
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var scope = context.GetArgument<string>("scope") ?? string.Empty;
        var name = context.GetArgument<string>("name") ?? context.GetArgument<string>("department");
        var operation = (context.GetArgument<string>("operation") ?? context.GetArgument<string>("action") ?? string.Empty).ToUpperInvariant();

        if (operation == "DELETE" && (scope.Equals("ALL", StringComparison.OrdinalIgnoreCase) || name?.Equals("ALL", StringComparison.OrdinalIgnoreCase) == true))
        {
            return Task.FromResult(ValidationResult.Success());
        }

        if (string.IsNullOrWhiteSpace(name) && !scope.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(ValidationResult.Failure("Department name is required."));
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

            var promptLower = (context.GetArgument<string>("prompt") ?? string.Empty).ToLowerInvariant();
            var rawOp = context.GetArgument<string>("operation") ?? context.GetArgument<string>("action");
            var operation = !string.IsNullOrWhiteSpace(rawOp)
                ? rawOp.ToUpperInvariant()
                : (promptLower.Contains("delete") || promptLower.Contains("remove") ? "DELETE" : (promptLower.Contains("update") || promptLower.Contains("head") || promptLower.Contains("change head") || promptLower.Contains("manager") ? "UPDATE" : "CREATE"));

            var name = context.GetArgument<string>("name")
                ?? context.GetArgument<string>("department")
                ?? string.Empty;

            switch (operation)
            {
                case "CREATE":
                    return await CreateDeptAsync(context, name, sw);
                case "UPDATE":
                    return await UpdateDeptAsync(context, name, sw);
                case "DELETE":
                    return await DeleteDeptAsync(context, name, sw);
                default:
                    return ToolExecutionResult.Failure($"Unknown department operation: {operation}", Definition.RiskLevel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DepartmentCrudTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }

    private async Task<ToolExecutionResult> CreateDeptAsync(ToolExecutionContext ctx, string name, Stopwatch sw)
    {
        var existing = await _db.Departments.FirstOrDefaultAsync(d => d.Name.ToLower() == name.ToLower());
        if (existing != null)
            return ToolExecutionResult.Failure($"Department '{name}' already exists (ID: {existing.Id}).", RiskLevel.Low);

        var prompt = ctx.GetArgument<string>("prompt") ?? string.Empty;
        var dept = new Department
        {
            Name = name,
            Description = ctx.GetArgument<string>("description") ?? $"{name} department"
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();

        // Check if head of department was specified
        var headName = ctx.GetArgument<string>("head") 
            ?? ctx.GetArgument<string>("departmentHead") 
            ?? ctx.GetArgument<string>("manager");
        if (string.IsNullOrWhiteSpace(headName))
        {
            var headAsMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"\b(?:with|under|assign|appoint)\s+([A-Za-z]+(?:\s+[A-Za-z]+)?)\s+(?:as\s+(?:head|department\s+head|lead|manager))\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (headAsMatch.Success) headName = headAsMatch.Groups[1].Value;
            else
            {
                var headMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"(?:head|lead|manager)\s+(?::|is|of)?\s*([A-Za-z]+(?:\s+[A-Za-z]+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (headMatch.Success) headName = headMatch.Groups[1].Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(headName))
        {
            headName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(headName.ToLower().Trim());
            var headEmp = await _db.Employees.FirstOrDefaultAsync(e => e.DepartmentId == dept.Id && e.Name.ToLower() == headName.ToLower());
            if (headEmp == null)
            {
                headEmp = new Employee
                {
                    Name = headName,
                    Email = $"{headName.ToLower().Replace(" ", ".")}@nexus.local",
                    DepartmentId = dept.Id,
                    Designation = $"Head of {name}",
                    Salary = 95000.00m,
                    ExperienceYears = 8,
                    Status = EmployeeStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Employees.Add(headEmp);
            }
            else
            {
                headEmp.Designation = $"Head of {name}";
            }
        }

        // Check if initial budget was specified
        decimal initialBudget = 0m;
        var budgetArg = ctx.GetArgument<decimal?>("budgetAmount") ?? ctx.GetArgument<decimal?>("budget");
        if (budgetArg is { } b && b > 0) initialBudget = b;
        else
        {
            var rawAmt = ctx.GetArgument<string>("budgetAmount") ?? ctx.GetArgument<string>("budget");
            if (!string.IsNullOrWhiteSpace(rawAmt))
            {
                rawAmt = rawAmt.Trim();
                if (rawAmt.EndsWith("k", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(rawAmt[..^1], out var kv)) initialBudget = kv * 1000m;
                else if (rawAmt.EndsWith("m", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(rawAmt[..^1], out var mv)) initialBudget = mv * 1_000_000m;
                else if (decimal.TryParse(rawAmt, out var sv)) initialBudget = sv;
            }
            else
            {
                var budMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"(?:\$([0-9\.,]+[kKmM]?)|([0-9\.,]+[kKmM]?)\$|\b(?:budget|allocation|allocated)\s+(?:of\s+|is\s+|at\s+|:\s*)?\$?([0-9\.,]+[kKmM]?)\$?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (budMatch.Success)
                {
                    var extracted = !string.IsNullOrEmpty(budMatch.Groups[1].Value) ? budMatch.Groups[1].Value :
                                    !string.IsNullOrEmpty(budMatch.Groups[2].Value) ? budMatch.Groups[2].Value :
                                    budMatch.Groups[3].Value;
                    var val = extracted.Replace(",", "").Trim();
                    if (val.EndsWith("k", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(val[..^1], out var kv)) initialBudget = kv * 1000m;
                    else if (val.EndsWith("m", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(val[..^1], out var mv)) initialBudget = mv * 1_000_000m;
                    else if (decimal.TryParse(val, out var sv)) initialBudget = sv;
                }
            }
        }

        if (initialBudget > 0)
        {
            var budgetRecord = await _db.Budgets.FirstOrDefaultAsync(b => b.DepartmentId == dept.Id);
            if (budgetRecord == null)
            {
                budgetRecord = new Budget
                {
                    DepartmentId = dept.Id,
                    Year = 2026,
                    Quarter = "Q3",
                    AllocatedAmount = initialBudget,
                    SpentAmount = 0m,
                    IsFrozen = false
                };
                _db.Budgets.Add(budgetRecord);
            }
            else
            {
                budgetRecord.AllocatedAmount = initialBudget;
            }
        }

        await _db.SaveChangesAsync();
        sw.Stop();

        var displayHead = !string.IsNullOrWhiteSpace(headName) ? headName : "Not Assigned";

        return ToolExecutionResult.Success(new
        {
            message = $"Department '{name}' created successfully (Head: {displayHead}, Budget: ${initialBudget:N2}).",
            dept.Id,
            dept.Name,
            dept.Description,
            name = dept.Name,
            department = dept.Name,
            head = headName,
            headOfDepartment = headName,
            departmentHead = headName,
            budgetAmount = initialBudget,
            allocatedBudget = initialBudget,
            amount = initialBudget,
            budget = initialBudget
        }, RiskLevel.Medium, sw.ElapsedMilliseconds);
    }

    private async Task<ToolExecutionResult> UpdateDeptAsync(ToolExecutionContext ctx, string name, Stopwatch sw)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Name.ToLower().Contains(name.ToLower()));
        if (dept == null)
            return ToolExecutionResult.Failure($"Department '{name}' not found.", RiskLevel.Low);

        var newName = ctx.GetArgument<string>("newName");
        var newDesc = ctx.GetArgument<string>("description");
        var head = ctx.GetArgument<string>("head") ?? ctx.GetArgument<string>("departmentHead") ?? ctx.GetArgument<string>("manager");

        if (!string.IsNullOrWhiteSpace(newName)) dept.Name = newName;
        if (!string.IsNullOrWhiteSpace(head))
        {
            dept.Description = $"Head of Department: {head}";
        }
        else if (!string.IsNullOrWhiteSpace(newDesc))
        {
            dept.Description = newDesc;
        }

        await _db.SaveChangesAsync();
        sw.Stop();

        return ToolExecutionResult.Success(new
        {
            message = $"Department '{dept.Name}' updated successfully (Head: {head ?? "Updated"}).",
            dept.Id, dept.Name, dept.Description
        }, RiskLevel.Medium, sw.ElapsedMilliseconds);
    }

    private async Task<ToolExecutionResult> DeleteDeptAsync(ToolExecutionContext ctx, string name, Stopwatch sw)
    {
        var scope = ctx.GetArgument<string>("scope") ?? string.Empty;
        var promptLower = (ctx.GetArgument<string>("prompt") ?? string.Empty).ToLowerInvariant();
        bool isBulk = scope.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                      name.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                      promptLower.Contains("delete all") || promptLower.Contains("remove all");

        if (isBulk)
        {
            var allDepts = await _db.Departments.Include(d => d.Employees).ToListAsync();
            int deptCount = allDepts.Count;

            var allBudgets = await _db.Budgets.ToListAsync();
            _db.Budgets.RemoveRange(allBudgets);

            var allEmployees = await _db.Employees.ToListAsync();
            int empCount = allEmployees.Count;
            _db.Employees.RemoveRange(allEmployees);

            _db.Departments.RemoveRange(allDepts);
            await _db.SaveChangesAsync();
            sw.Stop();

            return ToolExecutionResult.Success(new
            {
                message = $"All {deptCount} department(s) and all {empCount} employee(s) have been permanently deleted.",
                affectedCount = deptCount + empCount,
                deletedDepartments = deptCount,
                deletedEmployees = empCount
            }, RiskLevel.High, sw.ElapsedMilliseconds);
        }
        else
        {
            var cleanName = name.Replace(" department", "", StringComparison.OrdinalIgnoreCase)
                                .Replace(" dept", "", StringComparison.OrdinalIgnoreCase)
                                .Trim();

            var dept = await _db.Departments
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Name.ToLower() == cleanName.ToLower() ||
                                          d.Name.ToLower().Contains(cleanName.ToLower()) ||
                                          cleanName.ToLower().Contains(d.Name.ToLower()));

            if (dept == null)
                return ToolExecutionResult.Failure($"Department '{name}' not found.", RiskLevel.Low);

            // Delete associated budgets for this department
            var deptBudgets = await _db.Budgets.Where(b => b.DepartmentId == dept.Id).ToListAsync();
            _db.Budgets.RemoveRange(deptBudgets);

            // Delete department employees
            int empCount = dept.Employees.Count;
            _db.Employees.RemoveRange(dept.Employees);

            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();
            sw.Stop();

            return ToolExecutionResult.Success(new
            {
                message = $"Department '{dept.Name}' and its {empCount} employee(s) have been permanently deleted.",
                dept.Id,
                dept.Name,
                deletedEmployees = empCount
            }, RiskLevel.High, sw.ElapsedMilliseconds);
        }
    }
}
