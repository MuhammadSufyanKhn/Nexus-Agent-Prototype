using System;

namespace Nexus.Data.Events;

public class AgentActivityEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid AgentRunId { get; set; }
    public int StepNumber { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }

    public static AgentActivityEvent Create(
        Guid agentRunId,
        int stepNumber,
        string eventType,
        string message,
        string status = "Completed",
        string? details = null)
    {
        return new AgentActivityEvent
        {
            EventId = Guid.NewGuid(),
            AgentRunId = agentRunId,
            StepNumber = stepNumber,
            EventType = eventType,
            Message = message,
            Status = status,
            Timestamp = DateTime.UtcNow,
            Details = details
        };
    }
}
