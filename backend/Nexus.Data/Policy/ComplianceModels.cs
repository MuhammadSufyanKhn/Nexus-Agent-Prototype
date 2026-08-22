namespace Nexus.Data.Policies;

public enum ComplianceStatus
{
    COMPLIANT = 1,
    NON_COMPLIANT = 2,
    POLICY_NOT_FOUND = 3,
    INSUFFICIENT_INFORMATION = 4
}

public class ComplianceResult
{
    public string EmployeeName { get; set; } = string.Empty;
    public string ExpenseDescription { get; set; } = string.Empty;
    public string ExpenseType { get; set; } = string.Empty;
    public decimal ClaimedAmount { get; set; }
    public decimal AllowedAmount { get; set; }
    public decimal Difference { get; set; }
    public ComplianceStatus Status { get; set; } = ComplianceStatus.COMPLIANT;
    public string StatusName => Status.ToString();
    public string PolicySource { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
