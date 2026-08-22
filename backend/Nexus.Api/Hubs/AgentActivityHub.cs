using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Nexus.Api.Hubs;

public class AgentActivityHub : Hub
{
    public async Task JoinRunGroup(string agentRunId)
    {
        if (!string.IsNullOrWhiteSpace(agentRunId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, agentRunId);
        }
    }

    public async Task LeaveRunGroup(string agentRunId)
    {
        if (!string.IsNullOrWhiteSpace(agentRunId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, agentRunId);
        }
    }
}
