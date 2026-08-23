using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nexus.Agent.Intent;

public enum IntentType
{
    UNKNOWN = 0,

    // ── Employee ──────────────────────────────────────────────────────────────
    EMPLOYEE_CREATE = 1,
    EMPLOYEE_READ = 2,
    EMPLOYEE_UPDATE = 3,
    EMPLOYEE_DELETE = 4,
    EMPLOYEE_ONBOARDING = 7,

    // ── Budget & Finance ─────────────────────────────────────────────────────
    BUDGET_ANALYSIS = 5,     // read-only budget analytics / over-budget queries
    BUDGET_READ = 20,        // list budgets / show budget for a department
    BUDGET_UPDATE = 21,      // increase/decrease/set allocated budget

    // ── Expense ───────────────────────────────────────────────────────────────
    EXPENSE_COMPLIANCE = 6,  // check whether an expense complies with policy
    EXPENSE_READ = 22,       // list / show expenses
    EXPENSE_CREATE = 23,     // submit a new expense claim

    // ── Policy ────────────────────────────────────────────────────────────────
    POLICY_CREATE = 10,
    POLICY_READ = 11,
    POLICY_UPDATE = 12,
    POLICY_DELETE = 13,

    // ── Department ───────────────────────────────────────────────────────────
    DEPARTMENT_CREATE = 14,
    DEPARTMENT_READ = 15,
    DEPARTMENT_UPDATE = 16,
    DEPARTMENT_DELETE = 17,

    // ── Cross-cutting read intents ────────────────────────────────────────────
    APPROVAL_READ = 24,      // list / show pending or all approvals
    ONBOARDING_READ = 25,    // list onboarding tasks / status
    AUDIT_READ = 26,         // show audit log entries
    DASHBOARD_ANALYTICS = 27,// summary dashboard stats

    // ── SQL Agent passthrough ─────────────────────────────────────────────────
    SQL_AGENT = 8
}

public class StructuredEntities
{
    public string? EmployeeName { get; set; }
    public int? EmployeeId { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public decimal? Salary { get; set; }
    public decimal? Percentage { get; set; }
    public string? Status { get; set; }
    public string? SalaryFilter { get; set; }
    public string? Quarter { get; set; }
    public string? Email { get; set; }

    // Policy
    public string? PolicyCode { get; set; }
    public string? PolicyTitle { get; set; }
    public string? PolicyCategory { get; set; }

    // Budget
    public decimal? BudgetAmount { get; set; }
    public int? Year { get; set; }

    // Expense
    public decimal? ExpenseAmount { get; set; }
    public string? ExpenseType { get; set; }
}

public class ParsedIntentResult
{
    /// <summary>
    /// Canonical Intent string e.g. "CREATE_EMPLOYEE" or "EMPLOYEE_CREATE"
    /// </summary>
    public string Intent { get; set; } = "UNKNOWN";

    /// <summary>
    /// Primary target entity being acted upon: EMPLOYEE, DEPARTMENT, DEPARTMENT_BUDGET, POLICY, EXPENSE, ONBOARDING, APPROVAL, AUDIT_LOG
    /// </summary>
    public string TargetEntity { get; set; } = "UNKNOWN";

    /// <summary>
    /// Primary operation requested: CREATE, READ, LIST, SEARCH, UPDATE, DELETE, ANALYZE, ALLOCATE, APPROVE, REJECT, EXECUTE
    /// </summary>
    public string Operation { get; set; } = "UNKNOWN";

    public string Scope { get; set; } = "SINGLE"; // SINGLE, ALL, FILTERED, NONE

    [JsonIgnore]
    public IntentType ParsedIntentType
    {
        get
        {
            var raw = Intent?.Trim().ToUpperInvariant() ?? "";
            
            // Normalize action-first intent names to entity-first enum values
            raw = raw switch
            {
                "CREATE_EMPLOYEE" or "ADD_EMPLOYEE" or "REGISTER_EMPLOYEE" => "EMPLOYEE_CREATE",
                "GET_EMPLOYEES" or "LIST_EMPLOYEES" or "SEARCH_EMPLOYEES" or "READ_EMPLOYEES" => "EMPLOYEE_READ",
                "UPDATE_EMPLOYEE" or "MODIFY_EMPLOYEE" or "SET_EMPLOYEE_SALARY" or "UPDATE_EMPLOYEE_SALARY" => "EMPLOYEE_UPDATE",
                "DELETE_EMPLOYEE" or "REMOVE_EMPLOYEE" or "FIRE_EMPLOYEE" => "EMPLOYEE_DELETE",
                "ONBOARD_EMPLOYEE" or "EMPLOYEE_JOINING" => "EMPLOYEE_ONBOARDING",

                "CREATE_DEPARTMENT" or "ADD_DEPARTMENT" => "DEPARTMENT_CREATE",
                "GET_DEPARTMENTS" or "LIST_DEPARTMENTS" or "READ_DEPARTMENTS" => "DEPARTMENT_READ",
                "UPDATE_DEPARTMENT" or "MODIFY_DEPARTMENT" => "DEPARTMENT_UPDATE",
                "DELETE_DEPARTMENT" or "REMOVE_DEPARTMENT" => "DEPARTMENT_DELETE",

                "GET_DEPARTMENT_BUDGET" or "READ_BUDGET" or "GET_BUDGET" or "SHOW_BUDGET" => "BUDGET_READ",
                "UPDATE_DEPARTMENT_BUDGET" or "ALLOCATE_DEPARTMENT_BUDGET" or "ALLOCATE_BUDGET" or "UPDATE_BUDGET" => "BUDGET_UPDATE",
                "ANALYZE_BUDGET" or "BUDGET_ANALYTICS" or "OVER_BUDGET_ANALYSIS" => "BUDGET_ANALYSIS",

                "CREATE_POLICY" or "ADD_POLICY" or "UPLOAD_POLICY" => "POLICY_CREATE",
                "GET_POLICY" or "READ_POLICY" or "LIST_POLICIES" => "POLICY_READ",
                "UPDATE_POLICY" or "MODIFY_POLICY" => "POLICY_UPDATE",
                "DELETE_POLICY" or "REMOVE_POLICY" => "POLICY_DELETE",

                "CREATE_EXPENSE" or "SUBMIT_EXPENSE" => "EXPENSE_CREATE",
                "CHECK_EXPENSE" or "CHECK_EXPENSE_COMPLIANCE" or "EXPENSE_POLICY_CHECK" => "EXPENSE_COMPLIANCE",
                "GET_EXPENSES" or "READ_EXPENSES" or "LIST_EXPENSES" => "EXPENSE_READ",

                _ => raw
            };

            if (Enum.TryParse<IntentType>(raw, true, out var result))
                return result;

            return IntentType.UNKNOWN;
        }
    }

    public Dictionary<string, string> Entities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public StructuredEntities StructuredEntities { get; set; } = new();
    
    /// <summary>
    /// Parameters extracted for the target entity (e.g. name, salary, department, designation)
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Query filters for READ/LIST operations (e.g. department=IT, status=Active)
    /// </summary>
    public Dictionary<string, object> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> MissingFields { get; set; } = new();
    public bool RequiresPolicyLookup { get; set; } = false;
    public bool RequiresApproval { get; set; } = false;

    public double Confidence { get; set; } = 1.0;
    public bool RequiresClarification { get; set; } = false;
    public string? ClarificationPrompt { get; set; }
    public string RawPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Set when the LLM API call fails (not a fallback decision — an actual error).
    /// </summary>
    public string? LlmError { get; set; }
}

public class ParseIntentRequest
{
    public string Prompt { get; set; } = string.Empty;
}
