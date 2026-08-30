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

/// <summary>
/// Spec-aligned 4-state workflow state machine surfaced in the API response.
/// Maps directly to the Identity & Objective specification output contract.
/// </summary>
public enum WorkflowState
{
    /// <summary>Intent and parameters identified; awaiting user validation before any mutation.</summary>
    CONFIRMATION_REQUIRED,

    /// <summary>Input is ambiguous or critical parameters are missing — prompting user for more info.</summary>
    CLARIFICATION_REQUIRED,

    /// <summary>User explicitly confirmed; execution payload is populated and backend action is triggered.</summary>
    READY_TO_EXECUTE,

    /// <summary>Informational / read-only / conversational response — no backend action required.</summary>
    ANSWER_DIRECT
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

/// <summary>
/// Spec-defined confirmation block shown to the user before any mutation is executed.
/// Corresponds to the JSON field "confirmation_details" in the output contract.
/// </summary>
public class ConfirmationDetails
{
    /// <summary>Short description of what will be done, e.g. "Update employee compensation record in SQL Server"</summary>
    public string ProposedAction { get; set; } = string.Empty;

    /// <summary>Human-readable key-value summary, e.g. "Employee: Muhammad | New Amount: 150,000/mo | Effective Date: Next Month"</summary>
    public string ActionSummary { get; set; } = string.Empty;

    /// <summary>True when still awaiting user input (CONFIRMATION_REQUIRED / CLARIFICATION_REQUIRED); false once confirmed.</summary>
    public bool RequiresUserAction { get; set; } = true;
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

    /// <summary>
    /// Set when the Gemini LLM API itself failed (e.g. bad key, quota, model not found).
    /// Distinct from ErrorMessage which is a business/validation error.
    /// </summary>
    public string? LlmError { get; set; }

    public long ExecutionTimeMs { get; set; }

    // ── Spec-aligned Workflow State Machine fields ─────────────────────────────

    /// <summary>
    /// One of: CONFIRMATION_REQUIRED | CLARIFICATION_REQUIRED | READY_TO_EXECUTE | ANSWER_DIRECT.
    /// Drives the frontend rendering mode.
    /// </summary>
    public string State { get; set; } = WorkflowState.ANSWER_DIRECT.ToString();

    /// <summary>
    /// The clear, concise message displayed directly on the UI to the user.
    /// Maps to the spec's "user_message" field.
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// Summary of proposed action shown for confirmation. Maps to spec's "confirmation_details".
    /// </summary>
    public ConfirmationDetails? ConfirmationDetails { get; set; }

    /// <summary>
    /// Context-aware action choices presented to the HR user after command execution.
    /// </summary>
    public List<NextActionChoice> Choices { get; set; } = new();

    /// <summary>
    /// Target backend system: SQL_SERVER | N8N_WORKFLOW | ZAPIER | UNKNOWN.
    /// Maps to spec's parameters.target_system.
    /// </summary>
    public string TargetSystem { get; set; } = "SQL_SERVER";

    /// <summary>
    /// Populated only when state is READY_TO_EXECUTE. Contains the structured payload
    /// ready for the backend API/SQL tool call. Maps to spec's "execution_payload".
    /// </summary>
    public object? ExecutionPayload { get; set; }
}

public class NextActionChoice
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ActionType { get; set; } = "NAVIGATE"; // NAVIGATE | EXECUTE_PROMPT | OPEN_URL | MODAL
    public string? TargetTab { get; set; }
    public string? PromptToExecute { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string> Context { get; set; } = new();
}

public class ExecuteAgentRequest
{
    public string Prompt { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string UserRole { get; set; } = "Admin";
}
