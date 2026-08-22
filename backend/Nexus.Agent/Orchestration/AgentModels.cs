using System;
using System.Collections.Generic;
using Nexus.Data.Enums;

namespace Nexus.Agent.Orchestration;

public enum AgentState
{
    Initializing = 1,
    IntentParsed = 2,
    Planning = 3,
    Executing = 4,
    Completed = 5,
    Failed = 6
}

public enum AgentEventType
{
    REQUEST_RECEIVED = 1,
    INTENT_PARSED = 2,
    PLAN_GENERATED = 3,
    TOOL_SELECTED = 4,
    TOOL_STARTED = 5,
    TOOL_COMPLETED = 6,
    EXECUTION_COMPLETED = 7,
    EXECUTION_FAILED = 8
}

public class AgentEvent
{
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }

    public static AgentEvent Create(AgentEventType type, string message, string? details = null)
    {
        return new AgentEvent
        {
            EventType = type.ToString(),
            Timestamp = DateTime.UtcNow,
            Message = message,
            Details = details
        };
    }
}

public class AgentStep
{
    public int StepNumber { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public string Status { get; set; } = "Pending";
}

public class AgentPlan
{
    public List<AgentStep> Steps { get; set; } = new();
    public RiskLevel TotalRiskLevel { get; set; } = RiskLevel.Low;
    public int EstimatedStepsCount => Steps.Count;
}

public class AgentResult
{
    public Guid RunId { get; set; }
    public string OriginalPrompt { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public bool RequiresApproval { get; set; }
    public Nexus.Data.ActionPlan.ActionPlan? ActionPlan { get; set; }
    public AgentPlan Plan { get; set; } = new();
    public List<AgentStep> ExecutedSteps { get; set; } = new();
    public object? ResultData { get; set; }
    public List<AgentEvent> ExecutionFeed { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public long ExecutionTimeMs { get; set; }
}

public class ExecuteAgentRequest
{
    public string Prompt { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string UserRole { get; set; } = "Admin";
}
