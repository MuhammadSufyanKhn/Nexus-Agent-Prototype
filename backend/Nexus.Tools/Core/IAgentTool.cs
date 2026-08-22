using System.Threading.Tasks;

namespace Nexus.Tools.Core;

public interface IAgentTool
{
    ToolDefinition Definition { get; }
    Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context);
    Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context);
}
