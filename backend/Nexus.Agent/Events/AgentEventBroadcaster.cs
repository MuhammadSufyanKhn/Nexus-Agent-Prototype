using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexus.Data.Events;

namespace Nexus.Agent.Events;

public interface IAgentEventBroadcaster
{
    Task BroadcastEventAsync(AgentActivityEvent activityEvent, CancellationToken cancellationToken = default);
    Task<IEnumerable<AgentActivityEvent>> GetRunEventsAsync(Guid agentRunId, CancellationToken cancellationToken = default);
}

public class AgentEventBroadcaster : IAgentEventBroadcaster
{
    private readonly ILogger<AgentEventBroadcaster> _logger;
    private static readonly ConcurrentDictionary<Guid, List<AgentActivityEvent>> EventHistory = new();

    public AgentEventBroadcaster(ILogger<AgentEventBroadcaster> logger)
    {
        _logger = logger;
    }

    public Task BroadcastEventAsync(AgentActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        if (activityEvent == null) return Task.CompletedTask;

        _logger.LogInformation(
            "AGENT EVENT [{EventType}] (RunId: {RunId}, Step: {Step}): {Message}",
            activityEvent.EventType,
            activityEvent.AgentRunId,
            activityEvent.StepNumber,
            activityEvent.Message);

        var eventsList = EventHistory.GetOrAdd(activityEvent.AgentRunId, _ => new List<AgentActivityEvent>());
        lock (eventsList)
        {
            eventsList.Add(activityEvent);
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<AgentActivityEvent>> GetRunEventsAsync(Guid agentRunId, CancellationToken cancellationToken = default)
    {
        if (EventHistory.TryGetValue(agentRunId, out var eventsList))
        {
            lock (eventsList)
            {
                return Task.FromResult<IEnumerable<AgentActivityEvent>>(eventsList.ToList());
            }
        }

        return Task.FromResult<IEnumerable<AgentActivityEvent>>(Enumerable.Empty<AgentActivityEvent>());
    }
}
