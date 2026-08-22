using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nexus.Agent.Intent;

public enum IntentType
{
    UNKNOWN = 0,
    EMPLOYEE_CREATE = 1,
    EMPLOYEE_READ = 2,
    EMPLOYEE_UPDATE = 3,
    EMPLOYEE_DELETE = 4,
    BUDGET_ANALYSIS = 5,
    EXPENSE_COMPLIANCE = 6,
    EMPLOYEE_ONBOARDING = 7,
    /// <summary>
    /// Routes any natural-language database query directly to the Production SQL Agent
    /// (sql.analytics tool) — covers SELECT/INSERT/UPDATE/DELETE on DepartmentBudgets
    /// and other schema tables via the Gemini SQL Agent system prompt.
    /// </summary>
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
}

public class ParsedIntentResult
{
    public string Intent { get; set; } = "UNKNOWN";
    public string Scope { get; set; } = "SINGLE"; // SINGLE, ALL, FILTERED, NONE

    [JsonIgnore]
    public IntentType ParsedIntentType
    {
        get
        {
            if (Enum.TryParse<IntentType>(Intent, true, out var result))
            {
                return result;
            }
            return IntentType.UNKNOWN;
        }
    }

    public Dictionary<string, string> Entities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public StructuredEntities StructuredEntities { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double Confidence { get; set; } = 1.0;
    public bool RequiresClarification { get; set; } = false;
    public string? ClarificationPrompt { get; set; }
    public string RawPrompt { get; set; } = string.Empty;
}

public class ParseIntentRequest
{
    public string Prompt { get; set; } = string.Empty;
}
