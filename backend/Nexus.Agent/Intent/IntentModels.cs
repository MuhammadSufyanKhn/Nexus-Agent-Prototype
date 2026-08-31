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

    // ── Spec-aligned top-level intents ───────────────────────────────────────
    UPDATE_SALARY = 28,      // dedicated salary adjustment (maps to EMPLOYEE_UPDATE + salary params)
    SECURITY_TEST = 29,      // vulnerability check, connection audit, access review
    EXECUTE_AUTOMATION = 30, // trigger n8n flow, Zapier webhook, SQL stored procedure

    // ── Enterprise Workflow Intents ──────────────────────────────────────────
    EMPLOYEE_TRANSFER = 31,           // transfer employee to dept/manager/location
    EMPLOYEE_PROMOTE = 32,            // promote employee to role/title
    EMPLOYEE_OFFBOARD = 33,           // offboard/terminate employee
    EMPLOYEE_CANCEL_ONBOARDING = 34,  // cancel onboarding
    LEAVE_CREATE = 35,                // log sick day, PTO, vacation
    LEAVE_APPROVE = 36,               // approve leave request
    PAYROLL_HOLD = 37,                // hold payroll for division/dept
    PAYROLL_BONUS = 38,               // bulk bonus distribution
    BUDGET_REALLOCATE = 39,           // transfer budget between departments
    BUDGET_FREEZE = 40,               // freeze budget allocations
    SLACK_NOTIFY = 41,                // notify team on Slack
    SAP_SYNC = 42,                    // sync with SAP HCM
    BULK_EMPLOYEE_UPDATE = 43,        // bulk title/role change
    BULK_PAYROLL = 44,                // bulk contractor conversion / currency update
    EXPENSE_APPROVE = 45,             // approve expense claim
    POLICY_DISTRIBUTE = 46,           // broadcast policy update to staff
    MULTI_STEP_WORKFLOW = 47,         // sequential workflow (e.g. sick leave + slack)

    // ── Workplace Service Desk (IT Tickets) ──────────────────────────────────
    TICKET_CREATE = 50,
    TICKET_READ = 51,
    TICKET_TRIAGE = 52,
    TICKET_UPDATE = 53,

    // ── Candidate CV Screening & Job Openings ───────────────────────────────
    CV_SCREEN = 54,
    JOB_OPENING_CREATE = 57,
    JOB_OPENING_READ = 58,

    // ── Approval Decisions & Review ───────────────────────────────────────────
    APPROVAL_ACTION = 55,

    // ── Automated Workflows ───────────────────────────────────────────────────
    WORKFLOW_EXECUTE = 56,

    // ── SQL Agent passthrough ─────────────────────────────────────────────────
    SQL_AGENT = 8,

    // ── General AI Conversation / Greetings ───────────────────────────────────
    GENERAL_CONVERSATION = 99
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

    // Salary Update (spec: UPDATE_SALARY)
    public decimal? NewSalary { get; set; }
    public string? EffectiveDate { get; set; }

    // Workflow target system (spec: target_system)
    public string TargetSystem { get; set; } = "SQL_SERVER";

    // Automation (spec: EXECUTE_AUTOMATION)
    public string? AutomationTarget { get; set; }  // e.g. n8n flow name, Zapier hook URL
    public string? StoredProcedure { get; set; }
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

                "TRANSFER_EMPLOYEE" or "EMPLOYEE_TRANSFER" or "MOVE_EMPLOYEE" => "EMPLOYEE_TRANSFER",
                "PROMOTE_EMPLOYEE" or "EMPLOYEE_PROMOTE" or "PROMOTION" => "EMPLOYEE_PROMOTE",
                "OFFBOARD_EMPLOYEE" or "TERMINATE_EMPLOYEE" or "EXIT_CLEARANCE" => "EMPLOYEE_OFFBOARD",
                "CANCEL_ONBOARDING" or "CANCEL_ONBOARD" => "EMPLOYEE_CANCEL_ONBOARDING",
                "LOG_LEAVE" or "SICK_DAY" or "SICK_LEAVE" or "PTO_REQUEST" or "LEAVE_CREATE"
                    or "LOG_SICK_DAY" or "LOG_SICK_LEAVE" or "SICK_LEAVE_NOTIFY" or "SICK_DAY_SLACK"
                    or "LOG_LEAVE_SLACK" or "LEAVE_CREATE_SLACK" or "LOG_SICK_DAY_TODAY"
                    or "LOG_SICK_DAY_AND_NOTIFY" or "SICK_DAY_AND_NOTIFY" => "LEAVE_CREATE",
                "APPROVE_LEAVE" or "LEAVE_APPROVE" => "LEAVE_APPROVE",
                "HOLD_PAYROLL" or "PAYROLL_HOLD" => "PAYROLL_HOLD",
                "BULK_BONUS" or "DISTRIBUTE_BONUS" or "PAYROLL_BONUS" => "PAYROLL_BONUS",
                "REALLOCATE_BUDGET" or "BUDGET_REALLOCATE" or "TRANSFER_BUDGET" => "BUDGET_REALLOCATE",
                "FREEZE_BUDGET" or "BUDGET_FREEZE" => "BUDGET_FREEZE",
                "SLACK_NOTIFY" or "NOTIFY_SLACK" or "SEND_SLACK" => "SLACK_NOTIFY",
                "SAP_SYNC" or "SYNC_SAP" => "SAP_SYNC",
                "BULK_UPDATE_EMPLOYEE" or "BULK_EMPLOYEE_UPDATE" => "BULK_EMPLOYEE_UPDATE",
                "MULTI_STEP_WORKFLOW" or "COMPOSITE_WORKFLOW" => "MULTI_STEP_WORKFLOW",

                // ── Spec-aligned salary update ────────────────────────────────────
                "UPDATE_SALARY" or "SALARY_UPDATE" or "PAY_RAISE" or "SALARY_ADJUSTMENT"
                    or "INCREMENT" or "COMPENSATION_CHANGE" or "UPDATE_EMPLOYEE_COMPENSATION" => "UPDATE_SALARY",

                // ── Security / System Health ──────────────────────────────────────
                "SECURITY_TEST" or "SYSTEM_HEALTH" or "VULNERABILITY_CHECK"
                    or "CONNECTION_AUDIT" or "ACCESS_REVIEW" => "SECURITY_TEST",

                // ── Automation Execution ──────────────────────────────────────────
                "EXECUTE_AUTOMATION" or "TRIGGER_AUTOMATION" or "TRIGGER_N8N"
                    or "TRIGGER_ZAPIER" or "EXECUTE_WORKFLOW" or "RUN_AUTOMATION" => "EXECUTE_AUTOMATION",

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
                "APPROVE_EXPENSE" or "EXPENSE_APPROVE" => "EXPENSE_APPROVE",

                // ── Workplace Service Desk ─────────────────────────────────────────
                "CREATE_TICKET" or "REQUEST_HARDWARE" or "REQUEST_EQUIPMENT" or "IT_OPERATIONS"
                    or "HARDWARE_REQUEST" or "VPN_REQUEST" or "TICKET_CREATE" => "TICKET_CREATE",
                "GET_TICKETS" or "LIST_TICKETS" or "READ_TICKETS" or "SERVICE_DESK_READ"
                    or "TICKETS_READ" or "TICKET_READ" => "TICKET_READ",
                "TRIAGE_TICKET" or "SMART_TRIAGE" or "TICKET_TRIAGE" or "ASSIGN_TECHNICIAN" => "TICKET_TRIAGE",
                "RESOLVE_TICKET" or "UPDATE_TICKET" or "TICKET_UPDATE" => "TICKET_UPDATE",

                // ── Candidate CV Screening ─────────────────────────────────────────
                "SCREEN_CV" or "ANALYZE_CV" or "CV_ANALYSIS" or "CV_SCREEN"
                    or "EVALUATE_CANDIDATE" or "SCREEN_RESUME" or "CANDIDATE_SCREEN" => "CV_SCREEN",

                // ── Approvals ──────────────────────────────────────────────────────
                "APPROVE_ACTION" or "DECLINE_ACTION" or "REJECT_ACTION"
                    or "APPROVAL_ACTION" or "EXECUTE_APPROVAL" or "DECIDE_APPROVAL" => "APPROVAL_ACTION",

                // ── Automated Workflows ────────────────────────────────────────────
                "WORKFLOW_EXECUTE" or "RUN_WORKFLOW" or "EXECUTE_PIPELINE" or "SYNC_WORKFORCE" => "WORKFLOW_EXECUTE",

                "GENERAL_CONVERSATION" or "GREETING" or "CHAT" or "HI" or "HELLO" => "GENERAL_CONVERSATION",

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
    /// Friendly AI response text for conversational prompts (greetings, chit-chat, assistant questions)
    /// </summary>
    public string? ConversationalResponse { get; set; }

    /// <summary>
    /// Set when the LLM API call fails (not a fallback decision — an actual error).
    /// </summary>
    public string? LlmError { get; set; }
}

public class ParseIntentRequest
{
    public string Prompt { get; set; } = string.Empty;
}
