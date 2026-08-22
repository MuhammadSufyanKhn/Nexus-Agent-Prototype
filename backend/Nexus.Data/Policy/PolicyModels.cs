using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nexus.Data.Policies;

public enum PolicyDecisionStatus
{
    Approved = 1,
    Flagged = 2,
    Rejected = 3,
    InsufficientInformation = 4,
    ConflictDetected = 5
}

public class PolicyEvaluationContext
{
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = "IT";
    public int? ExperienceYears { get; set; }
    public decimal? RequestedSalary { get; set; }
    public string? ExpenseType { get; set; }
    public decimal? ExpenseAmount { get; set; }
}

public class PolicyDecisionResult
{
    public PolicyDecisionStatus Decision { get; set; } = PolicyDecisionStatus.Approved;
    public string DecisionName => Decision.ToString();
    public object? ProposedValue { get; set; }
    public decimal? MinAllowedValue { get; set; }
    public decimal? MaxAllowedValue { get; set; }
    public string PolicySource { get; set; } = string.Empty;
    public string PolicyCode { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public string PolicyEvidence { get; set; } = string.Empty;
}
