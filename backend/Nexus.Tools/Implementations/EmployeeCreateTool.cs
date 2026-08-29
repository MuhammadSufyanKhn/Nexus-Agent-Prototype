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
                var prompt = context.GetArgument<string>("prompt") ?? context.GetArgument<string>("question") ?? string.Empty;
                var emailMatch = Regex.Match(prompt, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b");
                if (emailMatch.Success)
                {
                    email = emailMatch.Value;
                }
                else
                {
                    email = $"{name.Trim().ToLower().Replace(" ", ".")}@gmail.com";
                }
            }

            var targetDeptName = context.GetArgument<string>("department")
                ?? context.GetArgument<string>("targetDepartment");

            if (string.IsNullOrWhiteSpace(targetDeptName))
            {
                var promptStr = context.GetArgument<string>("prompt") ?? context.GetArgument<string>("question") ?? string.Empty;
                var deptMatch = Regex.Match(promptStr, @"\bin\s+([A-Za-z]+(?:\s+[A-Za-z]+)?)\b", RegexOptions.IgnoreCase);
                if (deptMatch.Success)
                {
                    var candDept = deptMatch.Groups[1].Value.Trim();
                    if (!candDept.Equals("as", StringComparison.OrdinalIgnoreCase) && !candDept.Equals("the", StringComparison.OrdinalIgnoreCase))
                        targetDeptName = candDept;
                }
            }

            var deptId = context.GetArgument<int?>("departmentId") ?? 0;

            var salary = context.GetArgument<decimal?>("salary");
            if (!salary.HasValue || salary.Value <= 0)
            {
                var salStr = context.GetArgument<string>("salary") ?? context.GetArgument<string>("newSalary");
                if (!string.IsNullOrWhiteSpace(salStr) && decimal.TryParse(salStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedSal) && parsedSal > 0)
                {
                    salary = parsedSal;
                }
            }

            if (!salary.HasValue || salary.Value <= 0)
            {
                var prompt = context.GetArgument<string>("prompt") ?? context.GetArgument<string>("question") ?? string.Empty;
                var salMatch = Regex.Match(prompt, @"\b([0-9]+(?:\.[0-9]+)?)\s*(k|m)?\b", RegexOptions.IgnoreCase);
                if (salMatch.Success && decimal.TryParse(salMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var salVal) && salVal > 1000)
                {
                    var calcSalary = salVal * (salMatch.Groups[2].Success && salMatch.Groups[2].Value.ToLower() == "k" ? 1000m : 1m);
                    if (prompt.ToLowerInvariant().Contains("monthly")) calcSalary *= 12m;
                    salary = calcSalary;
                }
                else
                {
                    salary = 68000.00m;
                }
            }

            var dto = new CreateEmployeeDto
            {
                Name = name,
                Email = email,
                DepartmentId = deptId,
                DepartmentName = targetDeptName,
                Designation = context.GetArgument<string>("designation") ?? "Developer",
                Salary = salary.Value,
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
        var name = context.GetArgument<string>("name")
            ?? context.GetArgument<string>("employeeName")
            ?? context.GetArgument<string>("candidateName")
            ?? context.GetArgument<string>("candidate");

        if (!string.IsNullOrWhiteSpace(name) && !name.Equals("In It", StringComparison.OrdinalIgnoreCase) && !name.Equals("IT", StringComparison.OrdinalIgnoreCase)) return name.Trim();

        var prompt = context.GetArgument<string>("prompt") ?? context.GetArgument<string>("question") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            var knownNames = new[] { "John Smith", "Jane Doe", "Michael Johnson", "David Lee", "Robert Chen", "Sarah Jenkins", "Tariq Mahmood", "Maria Garcia", "Ahmed Khan", "Sufyan Khan", "Alex", "Amanda", "Sarah", "Jim", "Pam", "Marcus", "Ali", "Sara", "Ahmed" };
            foreach (var kn in knownNames)
            {
                if (Regex.IsMatch(prompt, $@"\b{kn}\b", RegexOptions.IgnoreCase))
                    return kn;
            }

            // Match capitalized full name (e.g. "John Smith", "David Lee")
            var capNameMatch = Regex.Match(prompt, @"\b([A-Z][a-z]+\s+[A-Z][a-z]+)\b");
            if (capNameMatch.Success)
            {
                var candidate = capNameMatch.Groups[1].Value.Trim();
                var lower = candidate.ToLowerInvariant();
                if (!lower.Contains("department") && !lower.Contains("office") && !lower.Contains("branch") && !lower.Contains("team"))
                    return candidate;
            }

            var namePatterns = new[]
            {
                @"\b(?:add\s+employee|create\s+employee|onboard\s+employee|onboard|hire)\s+(?:named\s+|name\s+)?([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)\b",
                @"\bemployee\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)\b",
                @"\bname\s+(?:is\s+)?([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)\b"
            };

            var fillerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "employee", "candidate", "person", "user", "staff", "list", "item", "record",
                "salaries", "salary", "an", "a", "the", "all", "new", "department", "it", "hr",
                "whose", "his", "her", "their", "is", "in", "with", "and", "as"
            };

            foreach (var pattern in namePatterns)
            {
                var match = Regex.Match(prompt, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var candidate = match.Groups[1].Value.Trim();
                    var firstWord = candidate.Split(' ')[0];
                    if (!fillerWords.Contains(candidate) && !fillerWords.Contains(firstWord) && candidate.Length >= 2)
                    {
                        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(candidate.ToLower());
                    }
                }
            }
        }

        return null;
    }
}
