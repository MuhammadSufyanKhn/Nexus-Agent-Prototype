using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nexus.Data.Services;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class EmployeeReadTool : IAgentTool
{
    private readonly IEmployeeService _employeeService;

    public EmployeeReadTool(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    public ToolDefinition Definition => new()
    {
        Name = "employee.read",
        Description = "Retrieves employee record(s) by ID, department, or search query.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""id"": { ""type"": ""integer"" },
            ""departmentId"": { ""type"": ""integer"" },
            ""search"": { ""type"": ""string"" }
          }
        }",
        RequiredPermission = "employee.read",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var id = context.GetArgument<int?>("id");
            if (id.HasValue && id.Value > 0)
            {
                var employee = await _employeeService.GetByIdAsync(id.Value);
                sw.Stop();
                if (employee == null) return ToolExecutionResult.Failure($"Employee with ID {id.Value} not found.", Definition.RiskLevel);
                return ToolExecutionResult.Success(employee, Definition.RiskLevel, sw.ElapsedMilliseconds);
            }

            var departmentId = context.GetArgument<int?>("departmentId");
            var search = context.GetArgument<string>("search");

            var employees = await _employeeService.GetAllAsync(departmentId, search);
            sw.Stop();
            return ToolExecutionResult.Success(employees, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
