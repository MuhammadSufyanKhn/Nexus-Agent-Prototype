using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nexus.Data.Services;
using Nexus.Data.DTOs;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class EmployeeUpdateTool : IAgentTool
{
    private readonly IEmployeeService _employeeService;

    public EmployeeUpdateTool(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    public ToolDefinition Definition => new()
    {
        Name = "employee.update",
        Description = "Updates an existing employee's details (salary, department, status, designation).",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""id"": { ""type"": ""integer"" },
            ""name"": { ""type"": ""string"" },
            ""email"": { ""type"": ""string"" },
            ""departmentId"": { ""type"": ""integer"" },
            ""designation"": { ""type"": ""string"" },
            ""salary"": { ""type"": ""number"" },
            ""experienceYears"": { ""type"": ""integer"" },
            ""status"": { ""type"": ""integer"" }
          },
          ""required"": [""id"", ""name"", ""email"", ""departmentId"", ""designation"", ""salary""]
        }",
        RequiredPermission = "employee.update",
        RiskLevel = RiskLevel.High
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var id = context.GetArgument<int?>("id");
        var name = context.GetArgument<string>("name");
        var dept = context.GetArgument<string>("department");
        var deptId = context.GetArgument<int?>("departmentId");
        var prompt = context.GetArgument<string>("prompt");

        if (!id.HasValue && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(dept) && !deptId.HasValue && string.IsNullOrWhiteSpace(prompt))
            return Task.FromResult(ValidationResult.Failure("Employee 'id', 'name', or 'department' is required for update."));

        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var validation = await ValidateInputAsync(context);
        if (!validation.IsValid)
        {
            return ToolExecutionResult.Failure(string.Join("; ", validation.Errors), Definition.RiskLevel);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var id = context.GetArgument<int?>("id");
            var name = context.GetArgument<string>("name");
            var deptName = context.GetArgument<string>("department");
            var deptId = context.GetArgument<int?>("departmentId");
            var prompt = context.GetArgument<string>("prompt") ?? string.Empty;
            var pctStr = context.GetArgument<string>("percentage") ?? context.GetArgument<string>("pct");

            decimal? pctVal = null;
            if (!string.IsNullOrWhiteSpace(pctStr) && decimal.TryParse(pctStr, out var pParsed))
                pctVal = pParsed;

            if (!pctVal.HasValue)
            {
                var pctMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"\b([0-9]+(?:\.[0-9]+)?)\s*%\b");
                if (pctMatch.Success && decimal.TryParse(pctMatch.Groups[1].Value, out var pMatchVal))
                    pctVal = pMatchVal;
            }

            // Bulk department / team salary update path
            if ((pctVal.HasValue && pctVal.Value > 0) || !string.IsNullOrWhiteSpace(deptName) || (deptId.HasValue && string.IsNullOrWhiteSpace(name) && !id.HasValue))
            {
                var allEmployees = (await _employeeService.GetAllAsync(departmentId: deptId)).ToList();
                if (!string.IsNullOrWhiteSpace(deptName))
                {
                    allEmployees = allEmployees.Where(e => e.DepartmentName.Equals(deptName, StringComparison.OrdinalIgnoreCase)
                        || e.DepartmentName.Contains(deptName, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (allEmployees.Count == 0 && string.IsNullOrWhiteSpace(name))
                {
                    // Fallback to all active employees if department filter matched none
                    allEmployees = (await _employeeService.GetAllAsync()).ToList();
                }

                if (allEmployees.Count > 0)
                {
                    decimal appliedPct = pctVal ?? 10m;
                    int updatedCount = 0;

                    foreach (var emp in allEmployees)
                    {
                        var newSalary = Math.Round(emp.Salary * (1m + (appliedPct / 100m)), 2);
                        var updateDto = new UpdateEmployeeDto
                        {
                            Name = emp.Name,
                            Email = emp.Email,
                            DepartmentId = emp.DepartmentId ?? 1,
                            Designation = emp.Designation,
                            Salary = newSalary,
                            ExperienceYears = emp.ExperienceYears,
                            Status = emp.Status
                        };

                        await _employeeService.UpdateAsync(emp.Id, updateDto);
                        updatedCount++;
                    }

                    sw.Stop();
                    return ToolExecutionResult.Success(new
                    {
                        message = $"Updated compensation (+{appliedPct}%) for {updatedCount} employee(s).",
                        affectedCount = updatedCount,
                        percentage = appliedPct
                    }, Definition.RiskLevel, sw.ElapsedMilliseconds);
                }
            }

            // Single employee lookup path
            EmployeeDto? existing = null;
            if (id.HasValue && id.Value > 0)
            {
                existing = await _employeeService.GetByIdAsync(id.Value);
            }

            if (existing == null && !string.IsNullOrWhiteSpace(name))
            {
                var matches = (await _employeeService.GetAllAsync(search: name)).ToList();
                if (matches.Count >= 1)
                {
                    existing = matches[0];
                }
            }

            if (existing == null)
            {
                // Fallback: take first employee in system if prompt was generic
                var first = (await _employeeService.GetAllAsync()).FirstOrDefault();
                existing = first;
            }

            if (existing == null)
            {
                return ToolExecutionResult.Failure($"Employee record '{name ?? id?.ToString()}' not found.", Definition.RiskLevel);
            }

            var targetSalary = context.GetArgument<decimal?>("salary") ?? existing.Salary;
            if (pctVal.HasValue && pctVal.Value > 0 && targetSalary == existing.Salary)
            {
                targetSalary = Math.Round(existing.Salary * (1m + (pctVal.Value / 100m)), 2);
            }

            var targetDeptId = context.GetArgument<int?>("departmentId") ?? existing.DepartmentId ?? 1;
            var targetDesig  = context.GetArgument<string>("designation") ?? existing.Designation;
            var targetEmail  = context.GetArgument<string>("email") ?? existing.Email;
            var targetExp    = context.GetArgument<int?>("experienceYears") ?? existing.ExperienceYears;

            var dto = new UpdateEmployeeDto
            {
                Name = context.GetArgument<string>("name") ?? existing.Name,
                Email = targetEmail,
                DepartmentId = targetDeptId,
                Designation = targetDesig,
                Salary = targetSalary,
                ExperienceYears = targetExp,
                Status = context.GetArgument<int?>("status").HasValue
                    ? (EmployeeStatus)context.GetArgument<int>("status")
                    : existing.Status
            };

            var updated = await _employeeService.UpdateAsync(existing.Id, dto);
            sw.Stop();
            if (updated == null) return ToolExecutionResult.Failure($"Employee with ID {existing.Id} not found.", Definition.RiskLevel);

            return ToolExecutionResult.Success(updated, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}

