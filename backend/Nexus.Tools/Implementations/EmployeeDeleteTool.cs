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
        var name = context.GetArgument<string>("name") ?? context.GetArgument<string>("employeeName");
        var scope = context.GetArgument<string>("scope") ?? string.Empty;
        var prompt = context.GetArgument<string>("prompt") ?? context.GetArgument<string>("question") ?? string.Empty;

        if (!id.HasValue && string.IsNullOrWhiteSpace(name) &&
            !scope.Equals("ALL", StringComparison.OrdinalIgnoreCase) &&
            !prompt.ToLowerInvariant().Contains("delete all") &&
            !prompt.ToLowerInvariant().Contains("remove all"))
        {
            return Task.FromResult(ValidationResult.Failure("Employee 'id', 'name', or bulk scope is required for deletion."));
        }

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
            var scope = context.GetArgument<string>("scope") ?? string.Empty;
            var prompt = (context.GetArgument<string>("prompt") ?? string.Empty).ToLowerInvariant();

            bool isBulk = scope.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                          prompt.Contains("delete all") || prompt.Contains("remove all");

            if (isBulk)
            {
                var allEmployees = (await _employeeService.GetAllAsync()).ToList();
                int deletedCount = 0;
                foreach (var emp in allEmployees)
                {
                    await _employeeService.DeleteAsync(emp.Id);
                    deletedCount++;
                }
                sw.Stop();
                return ToolExecutionResult.Success(new { deletedCount, message = $"Successfully deleted all {deletedCount} employee(s)." }, Definition.RiskLevel, sw.ElapsedMilliseconds);
            }

            if (!id.HasValue && !string.IsNullOrWhiteSpace(name))
            {
                var matches = (await _employeeService.GetAllAsync(search: name)).ToList();
                if (matches.Count >= 1)
                {
                    id = matches[0].Id;
                }
            }

            if (!id.HasValue)
            {
                return ToolExecutionResult.Failure($"No employee matching '{name}' found to delete.", Definition.RiskLevel);
            }

            var deleted = await _employeeService.DeleteAsync(id.Value);
            sw.Stop();

            if (!deleted) return ToolExecutionResult.Failure($"Employee with ID {id.Value} not found.", Definition.RiskLevel);
            return ToolExecutionResult.Success(new { deletedId = id.Value, message = "Employee successfully deleted." }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
