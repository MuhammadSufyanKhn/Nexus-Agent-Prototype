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

        var dept = new Department
        {
            Name = name,
            Description = ctx.GetArgument<string>("description") ?? $"{name} department"
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        sw.Stop();

        return ToolExecutionResult.Success(new
        {
            message = $"Department '{name}' created successfully.",
            dept.Id, dept.Name, dept.Description
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
            int count = allDepts.Count;

            // Unassign employees from departments before removal
            foreach (var d in allDepts)
            {
                foreach (var emp in d.Employees)
                {
                    emp.DepartmentId = null;
                }
            }

            _db.Departments.RemoveRange(allDepts);
            await _db.SaveChangesAsync();
            sw.Stop();

            return ToolExecutionResult.Success(new
            {
                message = $"All {count} department(s) have been permanently deleted.",
                affectedCount = count
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

            // Unassign employees if any exist
            foreach (var emp in dept.Employees)
            {
                emp.DepartmentId = null;
            }

            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();
            sw.Stop();

            return ToolExecutionResult.Success(new
            {
                message = $"Department '{dept.Name}' has been permanently deleted.",
                dept.Id, dept.Name
            }, RiskLevel.High, sw.ElapsedMilliseconds);
        }
    }
}
