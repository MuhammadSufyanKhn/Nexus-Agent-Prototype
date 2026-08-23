using System;
using System.Collections.Generic;
using Nexus.Data.Enums;

namespace Nexus.Data.ActionPlan;

public class ChangePreview
{
    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string? Difference { get; set; }
    public string ValueSource { get; set; } = "AI-proposed"; // AI-proposed, User-edited, Policy-derived, System-derived
    public bool IsEditable { get; set; } = true;
}

public class AffectedRecord
{
    public int RecordId { get; set; }
    public string EntityName { get; set; } = "Employee";
    public string PrimaryLabel { get; set; } = string.Empty;
    public List<ChangePreview> Changes { get; set; } = new();
}

public class ActionPlanStep
{
    public int StepNumber { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.High;
}

public class ActionPlan
{
    public Guid ApprovalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.High;
    public string Status { get; set; } = "AWAITING_APPROVAL";
    public decimal TotalFinancialImpact { get; set; }
    public int AffectedCount => AffectedRecords.Count;
    public List<AffectedRecord> AffectedRecords { get; set; } = new();
    public List<ActionPlanStep> Steps { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? Metadata { get; set; }
}

public class ApprovalDecisionRequest
{
    public Guid ApprovalId { get; set; }
    public bool Approved { get; set; }
    public string ApprovedBy { get; set; } = "Admin User";
    public string? Reason { get; set; }
    public Dictionary<string, object>? EditedParameters { get; set; }
}
