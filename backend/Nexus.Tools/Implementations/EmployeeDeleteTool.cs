using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nexus.Data.Services;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class EmployeeDeleteTool : IAgentTool
{
    private readonly IEmployeeService _employeeService;

    public EmployeeDeleteTool(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    public ToolDefinition Definition => new()
    {
        Name = "employee.delete",
        Description = "Deletes an employee record from the system. CRITICAL OPERATION.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""id"": { ""type"": ""integer"" }
          },
          ""required"": [""id""]
        }",
        RequiredPermission = "employee.delete",
        RiskLevel = RiskLevel.Critical
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var id = context.GetArgument<int?>("id");
        if (!id.HasValue || id.Value <= 0)
            return Task.FromResult(ValidationResult.Failure("Valid employee 'id' is required for deletion."));

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
            var deleted = await _employeeService.DeleteAsync(id);
            sw.Stop();

            if (!deleted) return ToolExecutionResult.Failure($"Employee with ID {id} not found.", Definition.RiskLevel);
            return ToolExecutionResult.Success(new { deletedId = id, message = "Employee successfully deleted." }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
