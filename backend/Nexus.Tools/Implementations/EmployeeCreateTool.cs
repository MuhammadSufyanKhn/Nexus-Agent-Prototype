using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nexus.Data.Services;
using Nexus.Data.DTOs;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class EmployeeCreateTool : IAgentTool
{
    private readonly IEmployeeService _employeeService;

    public EmployeeCreateTool(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    public ToolDefinition Definition => new()
    {
        Name = "employee.create",
        Description = "Creates a new employee record in the system.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""name"": { ""type"": ""string"" },
            ""email"": { ""type"": ""string"" },
            ""departmentId"": { ""type"": ""integer"" },
            ""designation"": { ""type"": ""string"" },
            ""salary"": { ""type"": ""number"" },
            ""experienceYears"": { ""type"": ""integer"" }
          },
          ""required"": [""name""]
        }",
        RequiredPermission = "employee.create",
        RiskLevel = RiskLevel.Medium
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var name = GetCandidateName(context);
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(ValidationResult.Failure("Argument 'name' is required."));

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
            var name = GetCandidateName(context)!;
            var email = context.GetArgument<string>("email");
            if (string.IsNullOrWhiteSpace(email))
            {
                email = $"{name.Trim().ToLower().Replace(" ", ".")}@nexus.local";
            }

            var deptId = context.GetArgument<int?>("departmentId") ?? 1;
            if (deptId <= 0) deptId = 1;

            var salary = context.GetArgument<decimal?>("salary") ?? 68000.00m;
            if (salary <= 0) salary = 68000.00m;

            var dto = new CreateEmployeeDto
            {
                Name = name,
                Email = email,
                DepartmentId = deptId,
                DepartmentName = context.GetArgument<string>("department"),
                Designation = context.GetArgument<string>("designation") ?? "Developer",
                Salary = salary,
                ExperienceYears = context.GetArgument<int?>("experienceYears") ?? 3,
                Status = EmployeeStatus.Active
            };

            var created = await _employeeService.CreateAsync(dto);
            sw.Stop();
            return ToolExecutionResult.Success(created, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }

    private static string? GetCandidateName(ToolExecutionContext context)
    {
        var name = context.GetArgument<string>("name");
        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();

        var prompt = context.GetArgument<string>("prompt") ?? context.GetArgument<string>("question") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            var match = Regex.Match(prompt, @"(?:create|onboard|for)\s*([a-zA-Z0-9]+(?:\s+[a-zA-Z0-9]+)?)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var raw = match.Groups[1].Value.Trim();
                var rawLower = raw.ToLower();
                if (!rawLower.StartsWith("a ") && !rawLower.StartsWith("as ") && !rawLower.StartsWith("in ") && !rawLower.StartsWith("new "))
                {
                    return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(rawLower);
                }
            }
        }

        return null;
    }
}
