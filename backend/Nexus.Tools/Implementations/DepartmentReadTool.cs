using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nexus.Data.Services;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class DepartmentReadTool : IAgentTool
{
    private readonly IDepartmentService _departmentService;

    public DepartmentReadTool(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    public ToolDefinition Definition => new()
    {
        Name = "department.read",
        Description = "Retrieves department record(s) and employee counts.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""id"": { ""type"": ""integer"" }
          }
        }",
        RequiredPermission = "department.read",
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
                var dept = await _departmentService.GetByIdAsync(id.Value);
                sw.Stop();
                if (dept == null) return ToolExecutionResult.Failure($"Department with ID {id.Value} not found.", Definition.RiskLevel);
                return ToolExecutionResult.Success(dept, Definition.RiskLevel, sw.ElapsedMilliseconds);
            }

            var departments = await _departmentService.GetAllAsync();
            sw.Stop();
            return ToolExecutionResult.Success(departments, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
