namespace Nexus.Data.Enums;

public enum EmployeeStatus
{
    Active = 1,
    Onboarding = 2,
    Inactive = 3,
    Terminated = 4
}

public enum ExpenseType
{
    Travel = 1,
    Meal = 2,
    Equipment = 3,
    Software = 4,
    Training = 5,
    Other = 6
}

public enum ExpenseStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Compliant = 4,
    NonCompliant = 5
}

public enum AgentRunStatus
{
    Pending = 1,
    Planning = 2,
    WaitingForApproval = 3,
    Executing = 4,
    Completed = 5,
    Failed = 6,
    Rejected = 7
}

public enum AgentActionStatus
{
    Pending = 1,
    Executing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4
}

public enum OnboardingTaskStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4
}
