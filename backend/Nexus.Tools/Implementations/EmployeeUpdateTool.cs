using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        var name = context.GetArgument<string>("name") ?? context.GetArgument<string>("employeeName");
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
            var name = context.GetArgument<string>("name") ?? context.GetArgument<string>("employeeName");
            var deptName = context.GetArgument<string>("department");
            var deptId = context.GetArgument<int?>("departmentId");
            var prompt = context.GetArgument<string>("prompt") ?? string.Empty;
            var pctStr = context.GetArgument<string>("percentage") ?? context.GetArgument<string>("pct");

            var invalidNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Designation", "Title", "Role", "Job Title", "Salary", "Compensation", "Status", "Email", "Department", "Dept"
            };

            if (!string.IsNullOrWhiteSpace(name) && invalidNames.Contains(name.Trim()))
            {
                name = null;
            }

            if (string.IsNullOrWhiteSpace(name) && !id.HasValue && !string.IsNullOrWhiteSpace(prompt))
            {
                var nameMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"(?:designation|salary|role|promotion|status|record|profile)\s+(?:for|of)?\s+([A-Za-z0-9\s]+?)\s+(?:to|in|at|\b)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (nameMatch.Success)
                {
                    var cand = nameMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(cand) && !invalidNames.Contains(cand))
                    {
                        name = cand;
                    }
                }
            }

            decimal? pctVal = null;
            if (!string.IsNullOrWhiteSpace(pctStr) && decimal.TryParse(pctStr, out var pParsed))
                pctVal = pParsed;

            if (!pctVal.HasValue && !string.IsNullOrWhiteSpace(prompt))
            {
                var pctMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"\b([0-9]+(?:\.[0-9]+)?)\s*%\b");
                if (pctMatch.Success && decimal.TryParse(pctMatch.Groups[1].Value, out var pMatchVal))
                    pctVal = pMatchVal;
            }

            bool isSingleEmployee = (id.HasValue && id.Value > 0) || !string.IsNullOrWhiteSpace(name);

            // Bulk department / team salary update path (ONLY when no specific employee is targeted)
            bool isBulkUpdate = !isSingleEmployee && ((pctVal.HasValue && pctVal.Value > 0) || (!string.IsNullOrWhiteSpace(deptName) && (pctVal.HasValue || prompt.Contains("raise", StringComparison.OrdinalIgnoreCase) || prompt.Contains("salary", StringComparison.OrdinalIgnoreCase) || prompt.Contains("bonus", StringComparison.OrdinalIgnoreCase))));

            if (isBulkUpdate)
            {
                var allEmployees = (await _employeeService.GetAllAsync(departmentId: deptId)).ToList();
                if (!string.IsNullOrWhiteSpace(deptName))
                {
                    allEmployees = allEmployees.Where(e => e.DepartmentName.Equals(deptName, StringComparison.OrdinalIgnoreCase)
                        || e.DepartmentName.Contains(deptName, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (allEmployees.Count == 0 && string.IsNullOrWhiteSpace(name))
                {
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

            if (existing == null && !string.IsNullOrWhiteSpace(prompt))
            {
                // Fallback: match by prompt containing employee name
                var allEmps = (await _employeeService.GetAllAsync()).ToList();
                existing = allEmps.FirstOrDefault(e => prompt.Contains(e.Name, StringComparison.OrdinalIgnoreCase));
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

            var targetSalary = context.GetArgument<decimal?>("salary") 
                ?? context.GetArgument<decimal?>("newSalary") 
                ?? existing.Salary;

            if (pctVal.HasValue && pctVal.Value > 0 && targetSalary == existing.Salary)
            {
                targetSalary = Math.Round(existing.Salary * (1m + (pctVal.Value / 100m)), 2);
            }

            var targetDeptId = context.GetArgument<int?>("departmentId") 
                ?? context.GetArgument<int?>("targetDepartmentId") 
                ?? existing.DepartmentId 
                ?? 1;

            var targetDesig = context.GetArgument<string>("designation") 
                ?? context.GetArgument<string>("role") 
                ?? context.GetArgument<string>("targetRole") 
                ?? context.GetArgument<string>("new_designation")
                ?? context.GetArgument<string>("newDesignation")
                ?? context.GetArgument<string>("title");

            if (string.IsNullOrWhiteSpace(targetDesig) || targetDesig.Equals(existing.Designation, StringComparison.OrdinalIgnoreCase))
            {
                // Extract from prompt if not explicitly in args, e.g. "Update Designation for Sufyan to Senior .NET Developer"
                var desigMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"(?:designation|role|title)\s+(?:of|for)?\s+[\w\s]+\s+to\s+([A-Za-z0-9\.\s\+\#\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (desigMatch.Success)
                {
                    var extracted = desigMatch.Groups[1].Value.Trim().TrimEnd('.', ',');
                    if (!string.IsNullOrWhiteSpace(extracted) && !extracted.Equals(existing.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        targetDesig = extracted;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(targetDesig))
            {
                targetDesig = existing.Designation;
            }

            var targetEmail = context.GetArgument<string>("email") ?? existing.Email;
            var targetExp = context.GetArgument<int?>("experienceYears") ?? existing.ExperienceYears;
            var targetName = context.GetArgument<string>("name") ?? context.GetArgument<string>("employeeName");
            if (string.IsNullOrWhiteSpace(targetName) || invalidNames.Contains(targetName.Trim()))
            {
                targetName = existing.Name;
            }

            var dto = new UpdateEmployeeDto
            {
                Name = targetName,
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

