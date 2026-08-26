using System;
using System.Collections.Generic;
using Nexus.Data.Enums;

namespace Nexus.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgentRun> AgentRuns { get; set; } = new List<AgentRun>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string Designation { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public int ExperienceYears { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    /// <summary>Employee's direct manager name (or ID reference)</summary>
    public string? ManagerName { get; set; }
    /// <summary>Physical office location or remote designation</summary>
    public string? Location { get; set; }
    /// <summary>Start date of employment</summary>
    public DateTime? StartDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Department? Department { get; set; }
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<OnboardingTask> OnboardingTasks { get; set; } = new List<OnboardingTask>();
    public ICollection<Leave> Leaves { get; set; } = new List<Leave>();
}

public class Budget
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public int Year { get; set; }
    public string Quarter { get; set; } = "Q3";
    public decimal AllocatedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    /// <summary>Whether this budget allocation is frozen (no spending/changes)</summary>
    public bool IsFrozen { get; set; } = false;
    public string? FreezeReason { get; set; }
    public DateTime? FrozenAt { get; set; }

    public Department? Department { get; set; }
}

public class Expense
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public ExpenseType ExpenseType { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Pending;
    public string Description { get; set; } = string.Empty;

    public Employee? Employee { get; set; }
}

public class Policy
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string ContentSummary { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AgentRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? UserId { get; set; }
    public string OriginalPrompt { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public AgentRunStatus Status { get; set; } = AgentRunStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public User? User { get; set; }
    public ICollection<AgentAction> Actions { get; set; } = new List<AgentAction>();
    public ICollection<Approval> Approvals { get; set; } = new List<Approval>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<OnboardingTask> OnboardingTasks { get; set; } = new List<OnboardingTask>();
}

public class AgentAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentRunId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public AgentActionStatus Status { get; set; } = AgentActionStatus.Pending;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public AgentRun? AgentRun { get; set; }
    public ICollection<Approval> Approvals { get; set; } = new List<Approval>();
}

public class Approval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentRunId { get; set; }
    public Guid? ActionId { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.High;
    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }

    public AgentRun? AgentRun { get; set; }
    public AgentAction? AgentAction { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid? AgentRunId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string PreviousHash { get; set; } = "GENESIS";
    public string CurrentHash { get; set; } = string.Empty;

    public AgentRun? AgentRun { get; set; }
    public User? User { get; set; }
}

public class OnboardingTask
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Guid? AgentRunId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string SystemTarget { get; set; } = string.Empty;
    public OnboardingTaskStatus Status { get; set; } = OnboardingTaskStatus.Pending;
    public string? DetailsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Employee? Employee { get; set; }
    public AgentRun? AgentRun { get; set; }
}

/// <summary>
/// Tracks employee leave requests: sick leave, PTO, vacation, etc.
/// </summary>
public class Leave
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; } = LeaveType.Other;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public string? ApprovedBy { get; set; }
    public string? Notes { get; set; }
    /// <summary>Reference to the agent run that created this leave</summary>
    public Guid? AgentRunId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public Employee? Employee { get; set; }
}

public class GeneratedDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DocumentType { get; set; } = string.Empty; // ONBOARDING_PACKAGE, OFFBOARDING_PACKAGE, ORIENTATION_SCHEDULE
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentHtml { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public Guid? AgentRunId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
