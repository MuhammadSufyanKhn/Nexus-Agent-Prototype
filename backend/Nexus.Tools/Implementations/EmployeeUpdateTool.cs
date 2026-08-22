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
        if (!id.HasValue || id.Value <= 0)
            return Task.FromResult(ValidationResult.Failure("Valid 'id' is required for employee update."));

        var name = context.GetArgument<string>("name");
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
            var id = context.GetArgument<int>("id");
            var dto = new UpdateEmployeeDto
            {
                Name = context.GetArgument<string>("name")!,
                Email = context.GetArgument<string>("email")!,
                DepartmentId = context.GetArgument<int>("departmentId"),
                Designation = context.GetArgument<string>("designation")!,
                Salary = context.GetArgument<decimal>("salary"),
                ExperienceYears = context.GetArgument<int>("experienceYears"),
                Status = (EmployeeStatus)context.GetArgument<int>("status")
            };

            var updated = await _employeeService.UpdateAsync(id, dto);
            sw.Stop();
            if (updated == null) return ToolExecutionResult.Failure($"Employee with ID {id} not found.", Definition.RiskLevel);

            return ToolExecutionResult.Success(updated, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
