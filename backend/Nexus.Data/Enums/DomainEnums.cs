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

// ── Leave Management ──────────────────────────────────────────────────────────
public enum LeaveType
{
    Sick = 1,
    PTO = 2,
    Vacation = 3,
    Maternity = 4,
    Paternity = 5,
    Emergency = 6,
    Unpaid = 7,
    Other = 8
}

public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

// ── Offboarding ──────────────────────────────────────────────────────────────
public enum OffboardType
{
    Offboard = 1,
    Terminate = 2,
    ExitClearance = 3,
    CancelOnboarding = 4,
    Resign = 5
}

// ── Payroll ──────────────────────────────────────────────────────────────────
public enum PayrollActionType
{
    Hold = 1,
    Resume = 2,
    BulkBonus = 3,
    IndividualBonus = 4,
    BulkConvert = 5,
    Sync = 6
}

public enum BudgetFreezeStatus
{
    Active = 1,
    Frozen = 2,
    Lifted = 3
}
