using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Agent.Orchestration;

public interface IAgentOrchestrator
{
    Task<AgentResult> ExecuteAsync(string userPrompt, int? userId = null, string userRole = "Admin", CancellationToken cancellationToken = default);
    Task<AgentResult> ResumeApprovedRunAsync(Guid runId, Guid approvalId, string approvedBy = "Executive Admin", System.Collections.Generic.Dictionary<string, object>? editedParameters = null, CancellationToken cancellationToken = default);
}
