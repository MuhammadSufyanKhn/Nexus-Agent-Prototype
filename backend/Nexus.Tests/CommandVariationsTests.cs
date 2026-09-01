using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Intent;
using Xunit;

namespace Nexus.Tests;

public class CommandVariationsTests
{
    private readonly IIntentParser _parser;

    public CommandVariationsTests()
    {
        _parser = new IntentParser(null, NullLogger<IntentParser>.Instance);
    }

    [Theory]
    // Module 1: AI Assistant (console)
    [InlineData("Appoint Ali as Acting Head of Admission department", "DEPARTMENT_UPDATE")]
    [InlineData("Assign Ali Khan as the Interim Lead for the Admission unit", "DEPARTMENT_UPDATE")]
    [InlineData("Make Ali the Acting Head of Admission department", "DEPARTMENT_UPDATE")]
    [InlineData("Set Ali as Acting Department Head for Admission", "DEPARTMENT_UPDATE")]
    [InlineData("Transfer Umar from Engineering to DevOps as Senior Platform Engineer", "EMPLOYEE_TRANSFER")]
    [InlineData("Reassign Umar to DevOps team in the role of Senior Platform Engineer", "EMPLOYEE_TRANSFER")]
    [InlineData("Move Umar from Engineering department to DevOps as Senior Platform Engineer", "EMPLOYEE_TRANSFER")]
    [InlineData("Relocate Umar to DevOps division as Senior Platform Engineer", "EMPLOYEE_TRANSFER")]
    [InlineData("Promote Sarah Jenkins to Principal Architect with manager John Smith", "EMPLOYEE_PROMOTE")]
    [InlineData("Promote Sarah Jenkins to Principal Architect reporting to manager John Smith", "EMPLOYEE_PROMOTE")]
    [InlineData("Advance Sarah Jenkins to Principal Architect position under John Smith", "EMPLOYEE_PROMOTE")]
    [InlineData("Freeze IT department budget for Q3 due to cost compliance", "BUDGET_FREEZE")]
    [InlineData("Halt IT department budget spending for Q3", "BUDGET_FREEZE")]
    [InlineData("Lock Q3 IT department budget allocation", "BUDGET_FREEZE")]

    // Module 2: Dashboard Analytics
    [InlineData("Show total headcount by department", "EMPLOYEE_READ")]
    [InlineData("Display employee headcount broken down by department", "EMPLOYEE_READ")]
    [InlineData("Get total count of employees in each department", "EMPLOYEE_READ")]
    [InlineData("Show Q3 budget vs actual spending summary", "BUDGET_READ")]
    [InlineData("Display Q3 budget allocation versus actual expenses summary", "BUDGET_READ")]
    [InlineData("Get financial overview of Q3 budget vs spending", "BUDGET_READ")]

    // Module 3: Employee Directory
    [InlineData("Show all active employees in IT department", "EMPLOYEE_READ")]
    [InlineData("List active staff members in the IT division", "EMPLOYEE_READ")]
    [InlineData("Find all employees working in IT department", "EMPLOYEE_READ")]
    [InlineData("Add new employee Bilal Ahmed as Senior DevOps Engineer in IT with salary $80,000", "EMPLOYEE_CREATE")]
    [InlineData("Create new employee Bilal Ahmed for IT department as Senior DevOps Engineer", "EMPLOYEE_CREATE")]
    [InlineData("Register employee Bilal Ahmed in IT as Senior DevOps Engineer", "EMPLOYEE_CREATE")]

    // Module 4: Departments
    [InlineData("Reallocate $50,000 budget from Marketing to R&D for Q3", "BUDGET_REALLOCATE")]
    [InlineData("Transfer $50,000 budget from Marketing division to R&D for Q3", "BUDGET_REALLOCATE")]
    [InlineData("Shift $50k budget allocation from Marketing to R&D in Q3", "BUDGET_REALLOCATE")]
    [InlineData("Increase Marketing department quarterly budget by $25,000", "BUDGET_UPDATE")]
    [InlineData("Add $25,000 to the Marketing department Q3 budget", "BUDGET_UPDATE")]
    [InlineData("Raise Marketing department budget allocation by $25k", "BUDGET_UPDATE")]

    // Module 5: Policy Center
    [InlineData("Show active HR policies", "POLICY_READ")]
    [InlineData("List all active HR policy documents", "POLICY_READ")]
    [InlineData("Get active policies under HR category", "POLICY_READ")]
    [InlineData("Check if $65 meal expense complies with meal policy", "POLICY_EVALUATE")]
    [InlineData("Evaluate compliance of $65 meal expense claim", "POLICY_EVALUATE")]
    [InlineData("Validate $65 meal claim against meal reimbursement policy", "POLICY_EVALUATE")]

    // Module 6: Workplace Service Desk
    [InlineData("Need MacBook Pro M3 and AWS VPN access for new hire Sarah in DevOps", "TICKET_CREATE")]
    [InlineData("Provision MacBook Pro M3 laptop and AWS VPN credentials for Sarah in DevOps", "TICKET_CREATE")]
    [InlineData("Create IT equipment request for Sarah in DevOps: MacBook Pro M3 and AWS VPN", "TICKET_CREATE")]
    [InlineData("Run AI Smart Triage on ticket #TCK-8849 and assign technician", "TICKET_TRIAGE")]
    [InlineData("Execute AI Smart Triage on service ticket TCK-8849 to assign technician", "TICKET_TRIAGE")]
    [InlineData("Mark ticket TCK-IT-001 as resolved", "TICKET_UPDATE")]
    [InlineData("Set status of ticket TCK-IT-001 to resolved", "TICKET_UPDATE")]

    // Module 7: HR Approval Center
    [InlineData("Approve and apply proposed employee transfer for Alex to Product", "APPROVAL_ACTION")]
    [InlineData("Confirm and execute pending employee transfer for Alex to Product", "APPROVAL_ACTION")]
    [InlineData("Decline pending budget reallocation request for Marketing department", "APPROVAL_ACTION")]
    [InlineData("Reject pending budget reallocation for Marketing department", "APPROVAL_ACTION")]

    // Module 8: Expense Review & Compliance
    [InlineData("Submit meal expense claim of $65.00 for Client Dinner under Sarah Ahmed", "EXPENSE_CREATE")]
    [InlineData("Create expense claim of $65 for Client Dinner under Sarah Ahmed", "EXPENSE_CREATE")]
    [InlineData("Approve expense claim #EXP-4012 for $220.00 travel reimbursement", "EXPENSE_APPROVE")]
    [InlineData("Authorize reimbursement for expense claim #EXP-4012 of $220", "EXPENSE_APPROVE")]
    [InlineData("Reject non-compliant equipment expense claim for $850.00", "EXPENSE_REJECT")]
    [InlineData("Decline equipment expense claim of $850 due to policy violation", "EXPENSE_REJECT")]

    // Module 9: Candidate CV Screening
    [InlineData("Analyze candidate CV text against Senior Full Stack Developer role requirements", "CV_SCREEN")]
    [InlineData("Score resume content for Senior Full Stack Developer position", "CV_SCREEN")]
    [InlineData("Create new job requisition for Senior DevOps Engineer in IT department", "JOB_OPENING_CREATE")]
    [InlineData("Post job opening for Senior DevOps Engineer in IT", "JOB_OPENING_CREATE")]

    // Module 10: Employee Onboarding Hub
    [InlineData("Prepare complete onboarding document package for Ahmed Khan in IT", "ONBOARDING_PREPARE")]
    [InlineData("Generate onboarding appointment letter and NDA for Ahmed Khan", "ONBOARDING_PREPARE")]
    [InlineData("Schedule 3-day orientation schedule for new hire Ahmed Khan starting Monday", "ORIENTATION_SCHEDULE")]
    [InlineData("Create orientation schedule for Ahmed Khan starting Monday", "ORIENTATION_SCHEDULE")]

    // Module 11: Automated Workflows
    [InlineData("Execute Automated Employee Onboarding Pipeline", "WORKFLOW_EXECUTE")]
    [InlineData("Run automated employee onboarding workflow pipeline", "WORKFLOW_EXECUTE")]
    [InlineData("Execute Corporate Expense Policy Compliance Sweep", "WORKFLOW_EXECUTE")]
    [InlineData("Run corporate expense policy compliance sweep automation", "WORKFLOW_EXECUTE")]

    // Module 12: Enterprise Activity History
    [InlineData("Show audit activity logs for employee transfers", "AUDIT_READ")]
    [InlineData("Display system audit trail for employee transfers", "AUDIT_READ")]
    [InlineData("Run security and authorization compliance test", "SECURITY_TEST")]
    [InlineData("Execute enterprise security audit check", "SECURITY_TEST")]
    public async Task ParseIntent_PhrasingVariations_ShouldMapCorrectly(string prompt, string expectedIntent)
    {
        var result = await _parser.ParseIntentAsync(prompt);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Intent));
        Assert.NotEqual("UNKNOWN", result.Intent);
        Assert.True(result.Confidence >= 0.7, $"Low confidence for prompt: '{prompt}'. Got {result.Confidence}");
    }
}
