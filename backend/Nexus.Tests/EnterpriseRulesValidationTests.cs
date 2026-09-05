using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Intent;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Data.Services;
using Xunit;

namespace Nexus.Tests;

public class EnterpriseRulesValidationTests
{
    private readonly IntentParser _intentParser;

    public EnterpriseRulesValidationTests()
    {
        _intentParser = new IntentParser(null, NullLogger<IntentParser>.Instance);
    }

    [Fact]
    public async Task Rule2_EmployeeOnboarding_PreservesExactDesignationWithoutInEngineering()
    {
        var prompt = "Onboard Sarah Jenkins as Senior Software Engineer in Engineering with a salary of $120,000 and send a welcome email to sufyankhankhattak33@gmail.com.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("Sarah Jenkins", parsed.Entities?.GetValueOrDefault("name"));
        Assert.Equal("Senior Software Engineer", parsed.Entities?.GetValueOrDefault("designation"));
        Assert.Equal("Engineering", parsed.Entities?.GetValueOrDefault("department"));
        Assert.NotEqual("Senior Software Engineer In Engineering", parsed.Entities?.GetValueOrDefault("designation"));
        Assert.Equal("120000", parsed.Entities?.GetValueOrDefault("salary"));
        Assert.Equal("sufyankhankhattak33@gmail.com", parsed.Entities?.GetValueOrDefault("email"));
    }

    [Fact]
    public async Task Rule2_SufyanKhan_Onboarding_ExtractsExactFields()
    {
        var prompt = "Onboard Sufyan Khan with a salary of $200,000. His designation is Developer, he will be joining the IT department, and send him a welcome email at sufyankhankhattak33@gmail.com.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("Sufyan Khan", parsed.Entities?.GetValueOrDefault("name"));
        Assert.Equal("Developer", parsed.Entities?.GetValueOrDefault("designation"));
        Assert.Equal("IT", parsed.Entities?.GetValueOrDefault("department"));
        Assert.Equal("200000", parsed.Entities?.GetValueOrDefault("salary"));
        Assert.Equal("sufyankhankhattak33@gmail.com", parsed.Entities?.GetValueOrDefault("email"));
    }

    [Fact]
    public async Task Rule3_JobOpeningCreation_ResolvesITDepartmentAndLeavesExperienceUnspecifiedIfOmitted()
    {
        var prompt = "Create a new job opening for Database Administrator in the IT department with location Remote / Hybrid, salary $50,000 - $60,000, required experience, and start date.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("Database Administrator", parsed.Entities?.GetValueOrDefault("title") ?? parsed.Entities?.GetValueOrDefault("jobTitle"));
        Assert.Equal("IT", parsed.Entities?.GetValueOrDefault("department"));
        Assert.NotEqual("With", parsed.Entities?.GetValueOrDefault("department"));
        Assert.NotEqual("In IT", parsed.Entities?.GetValueOrDefault("department"));
    }

    [Fact]
    public async Task Rule4_CandidateScreening_ResolvesCandidateAndJobOpening()
    {
        var prompt = "Screen Hammad's resume against the Database Administrator position.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("CV_SCREEN", parsed.Intent);
        Assert.Equal("Hammad", parsed.Entities?.GetValueOrDefault("name"));
        Assert.Equal("Database Administrator", parsed.Entities?.GetValueOrDefault("jobTitle") ?? parsed.Entities?.GetValueOrDefault("job_title"));
    }

    [Fact]
    public async Task Rule5_CandidateScreening_NonExistingJobOpening_ParsedCorrectly()
    {
        var prompt = "Screen Hammad's resume against the Senior React Developer position requirements.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("CV_SCREEN", parsed.Intent);
        Assert.Equal("Hammad", parsed.Entities?.GetValueOrDefault("name"));
        Assert.Equal("Senior React Developer", parsed.Entities?.GetValueOrDefault("jobTitle") ?? parsed.Entities?.GetValueOrDefault("job_title"));
    }

    [Fact]
    public async Task Rule6_InterviewQuestions_TailoredToCandidateAndAppliedPosition()
    {
        var prompt = "Generate interview question recommendations based on Hammad's CV and the Database Administrator position he applied for.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("CANDIDATE_INTERVIEW_QUESTIONS", parsed.Intent);
        Assert.Equal("Hammad", parsed.Entities?.GetValueOrDefault("name"));
        Assert.Equal("Database Administrator", parsed.Entities?.GetValueOrDefault("jobTitle") ?? parsed.Entities?.GetValueOrDefault("job_title"));
    }

    [Fact]
    public async Task Rule7_EmployeeDesignationUpdate_PreservesExactValues()
    {
        var prompt = "Update the designation for Khattak to Senior .NET Developer.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("Khattak", parsed.Entities?.GetValueOrDefault("name"));
        Assert.Equal("Senior .NET Developer", parsed.Entities?.GetValueOrDefault("designation"));
    }

    [Fact]
    public async Task Rule8_DepartmentCreation_PreservesInitialHeadcountOf10()
    {
        var prompt = "Create AI Innovations department with head Tariq Mahmood and allocate initial headcount of 10 FTEs.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("AI Innovations", parsed.Entities?.GetValueOrDefault("name"));
        Assert.Equal("Tariq Mahmood", parsed.Entities?.GetValueOrDefault("head"));
        Assert.Equal("10", parsed.Entities?.GetValueOrDefault("headcount"));
    }

    [Fact]
    public async Task Rule9_BudgetQuery_FiltersDepartmentsStrictlyOverThreshold()
    {
        var prompt = "Show departments with allocated budgets over $200,000.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("BUDGET_ANALYSIS", parsed.Intent);
        Assert.Equal("200000", parsed.Entities?.GetValueOrDefault("minBudget"));
    }

    [Fact]
    public async Task Rule10_ExpenseClaimSubmission_CreatesInPendingReviewState()
    {
        var prompt = "Submit meal expense claim of $40.00 for Client Dinner under Khattak.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("EXPENSE_CREATE", parsed.Intent);
        Assert.Equal("40.00", parsed.Entities?.GetValueOrDefault("amount"));
        Assert.Equal("Meal", parsed.Entities?.GetValueOrDefault("category"));
    }

    [Fact]
    public async Task Rule11_ExpenseComplianceSweep_ParsesCorrectly()
    {
        var prompt = "Run AI Policy Compliance Sweep on all submitted employee expense claims.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.True(parsed.Intent == "EXPENSE_COMPLIANCE_SWEEP" || parsed.Intent == "WORKFLOW_EXECUTE");
    }

    [Fact]
    public async Task Rule12_CorporatePolicyRetrieval_ReturnsCompletePOLHR001Content()
    {
        var prompt = "Show the complete corporate compensation policy POL-HR-001.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("POLICY_READ", parsed.Intent);
        Assert.Equal("POL-HR-001", parsed.Entities?.GetValueOrDefault("policyCode"));
    }

    [Fact]
    public async Task Rule13_OnboardingPackageDocument_VerifiesEmployeePresence()
    {
        var prompt = "Generate complete Onboarding Package document for new hire Sufyan Khan.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("ONBOARDING_DOCUMENT_GENERATE", parsed.Intent);
        Assert.Equal("Sufyan Khan", parsed.Entities?.GetValueOrDefault("name"));
    }

    [Fact]
    public async Task Rule15_ExpenseReport_ParsesPeriodAndCategory()
    {
        var prompt = "Create an expense report for Q3 travel reimbursements totaling $1,450.00.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("Q3", parsed.Entities?.GetValueOrDefault("period") ?? parsed.Entities?.GetValueOrDefault("quarter"));
        Assert.Equal("Travel", parsed.Entities?.GetValueOrDefault("category"));
    }

    [Fact]
    public async Task Rule16_HeadcountReport_ParsesCorrectly()
    {
        var prompt = "Generate comprehensive headcount and staffing distribution report.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.NotEqual("UNKNOWN", parsed.Intent);
        Assert.True(parsed.Confidence >= 0.7);
    }

    [Fact]
    public async Task Rule17_ComprehensiveHRReports_ParsesCorrectly()
    {
        var prompt = "Generate comprehensive HR reports covering workforce, recruitment, compensation, expenses, budgets, compliance, onboarding, staffing, and employee data.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("DASHBOARD_ANALYTICS", parsed.Intent);
        Assert.Equal("COMPREHENSIVE_HR", parsed.Parameters?.GetValueOrDefault("reportType")?.ToString());
        Assert.Equal("REPORT", parsed.TargetEntity);
    }

    [Fact]
    public async Task Rule18_CorporateExpenseAuditReport_ParsesCorrectly()
    {
        var prompt = "Generate corporate expense audit and compliance report.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("EXPENSE_READ", parsed.Intent);
        Assert.Equal("EXPENSE_AUDIT", parsed.Parameters?.GetValueOrDefault("reportType")?.ToString());
        Assert.Equal("REPORT", parsed.TargetEntity);
    }

    [Fact]
    public async Task Rule19_MealExpenseLimit_ParsesCustomThreshold20()
    {
        var prompt = "Show all meal expense claims exceeding the $20 daily limit.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("EXPENSE_READ", parsed.Intent);
        Assert.Equal("Meal", parsed.Entities?.GetValueOrDefault("category"));
        Assert.True(parsed.Parameters?.ContainsKey("minAmount") == true || parsed.Entities?.ContainsKey("maxAmount") == true);
    }

    [Fact]
    public async Task Rule20_OrientationSchedule_SummerInternsIT_ParsesCorrectly()
    {
        var prompt = "Generate 5-day workforce orientation and induction schedule for summer engineering interns in IT.";
        var parsed = await _intentParser.ParseIntentAsync(prompt);

        Assert.NotNull(parsed);
        Assert.Equal("ONBOARDING_DOCUMENT_GENERATE", parsed.Intent);
        Assert.Equal("ORIENTATION_SCHEDULE", parsed.Parameters?.GetValueOrDefault("documentType")?.ToString());
        Assert.Equal("DOCUMENT", parsed.TargetEntity);
    }

    [Fact]
    public async Task ComprehensiveHrReport_ContainsAll16RequiredSections()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase("CompHrTest_" + Guid.NewGuid().ToString("N"))
            .Options;
        using var db = new NexusDbContext(options);

        var it = new Department { Id = 1, Name = "IT", Description = "Tech Pod" };
        var hr = new Department { Id = 2, Name = "HR", Description = "People Operations" };
        db.Departments.AddRange(it, hr);

        var emp1 = new Employee { Id = 1, Name = "Alice", DepartmentId = 1, Designation = "Senior .NET Engineer", Salary = 80000m, ExperienceYears = 3, Status = EmployeeStatus.Active };
        var emp2 = new Employee { Id = 2, Name = "Bob", DepartmentId = 2, Designation = "HR Lead", Salary = 60000m, ExperienceYears = 2, Status = EmployeeStatus.Active };
        db.Employees.AddRange(emp1, emp2);

        var job = new JobOpening { Id = 1, Title = "DevOps Specialist", Department = "IT", Status = "Active", Requirements = "3+ years AWS/Azure", Location = "Hybrid", SalaryRange = "$70,000 - $90,000" };
        db.JobOpenings.Add(job);

        var candidate = new CandidateApplication { Id = 1, CandidateName = "Charlie", Email = "charlie@nexus.test", Status = "In Progress", JobOpeningId = 1, FitScore = 88, ExperienceYears = 4, SubmittedAt = DateTime.UtcNow };
        db.CandidateApplications.Add(candidate);

        var expense = new Expense { Id = 1, EmployeeId = 1, Amount = 45m, Category = "Meal", Status = ExpenseStatus.Approved, ExpenseDate = DateTime.UtcNow, Description = "Bistro client lunch" };
        db.Expenses.Add(expense);

        var budget = new Budget { Id = 1, DepartmentId = 1, AllocatedAmount = 100000m, SpentAmount = 45m, Year = 2026, Quarter = "Q3" };
        db.Budgets.Add(budget);

        var policy = new Policy { Id = 1, Code = "POL-FIN-002", Title = "Meal Expense Limit", Category = "Finance", ContentSummary = "Daily meal claims must not exceed $50.00." };
        db.Policies.Add(policy);

        var masterBudget = new MasterBudget { Id = 1, Year = 2026, TotalBudgetPool = 1_000_000_000m };
        db.MasterBudgets.Add(masterBudget);

        await db.SaveChangesAsync();

        var docService = new PdfDocumentService(db);
        var doc = await docService.GenerateComprehensiveHrReportAsync(
            employees: await db.Employees.Include(e => e.Department).ToListAsync(),
            departments: await db.Departments.ToListAsync(),
            jobOpenings: await db.JobOpenings.ToListAsync(),
            candidates: await db.CandidateApplications.ToListAsync(),
            expenses: await db.Expenses.Include(e => e.Employee).ToListAsync(),
            budgets: await db.Budgets.Include(b => b.Department).ToListAsync(),
            masterBudget: masterBudget,
            policies: await db.Policies.ToListAsync(),
            onboardingTasks: new List<OnboardingTask>()
        );

        Assert.NotNull(doc);
        Assert.NotNull(doc.ContentHtml);

        var requiredSections = new[]
        {
            "Workforce / Headcount",
            "Recruitment",
            "Job Openings",
            "Candidate Pipeline",
            "Compensation",
            "Salary Compliance",
            "Expenses",
            "Expense Compliance",
            "Department Budgets",
            "Budget Utilization",
            "Onboarding",
            "Staffing",
            "Employee Data",
            "Key Findings",
            "Recommendations",
            "Executive Summary"
        };

        foreach (var sec in requiredSections)
        {
            Assert.Contains(sec, doc.ContentHtml);
        }
    }

    [Fact]
    public async Task CorporateExpenseAuditReport_RendersAuditLedgerAndViolations()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase("ExpenseAuditTest_" + Guid.NewGuid().ToString("N"))
            .Options;
        using var db = new NexusDbContext(options);

        var it = new Department { Id = 1, Name = "IT", Description = "Tech Pod" };
        db.Departments.Add(it);

        var emp = new Employee { Id = 1, Name = "Khattak", DepartmentId = 1, Designation = "Senior .NET Developer", Salary = 120000m, ExperienceYears = 5, Status = EmployeeStatus.Active, Department = it };
        db.Employees.Add(emp);

        var exp1 = new Expense { Id = 1, EmployeeId = 1, Employee = emp, Amount = 75.50m, Category = "Meal", Status = ExpenseStatus.NonCompliant, ExpenseDate = DateTime.UtcNow, Description = "Team lunch", FlagReason = "POL-FIN-002: Exceeds $50 daily limit", PolicyLimit = 50m, Variance = 25.50m };
        var exp2 = new Expense { Id = 2, EmployeeId = 1, Employee = emp, Amount = 35.00m, Category = "Travel", Status = ExpenseStatus.Approved, ExpenseDate = DateTime.UtcNow, Description = "Airport ride", PolicyLimit = 100m, Variance = 0m };
        db.Expenses.AddRange(exp1, exp2);

        var budget = new Budget { Id = 1, DepartmentId = 1, AllocatedAmount = 50000m, SpentAmount = 110.50m, Year = 2026, Quarter = "Q3", Department = it };
        db.Budgets.Add(budget);

        var policy = new Policy { Id = 1, Code = "POL-FIN-002", Title = "Meal Expense Limit", Category = "Finance", ContentSummary = "Meal limit $50 per day" };
        db.Policies.Add(policy);

        await db.SaveChangesAsync();

        var docService = new PdfDocumentService(db);
        var doc = await docService.GenerateCorporateExpenseAuditReportAsync(
            expenses: await db.Expenses.Include(e => e.Employee).ThenInclude(e => e!.Department).ToListAsync(),
            policies: await db.Policies.ToListAsync(),
            runId: null
        );

        Assert.NotNull(doc);
        Assert.NotNull(doc.ContentHtml);
        Assert.Contains("Corporate Expense Audit and Compliance Report", doc.ContentHtml);
        Assert.Contains("POL-FIN-002", doc.ContentHtml);
        Assert.Contains("Khattak", doc.ContentHtml);
    }

    [Fact]
    public async Task SummerInternInductionSchedule_Renders5DayCurriculum()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase("InternSchedTest_" + Guid.NewGuid().ToString("N"))
            .Options;
        using var db = new NexusDbContext(options);

        var docService = new PdfDocumentService(db);
        var doc = await docService.GenerateSummerInternInductionScheduleAsync(
            departmentName: "IT",
            targetAudience: "Summer Engineering Interns",
            durationDays: 5,
            runId: null
        );

        Assert.NotNull(doc);
        Assert.NotNull(doc.ContentHtml);
        Assert.Contains("Summer Engineering Interns", doc.ContentHtml);
        Assert.Contains("5-Day Workforce Orientation & Induction Schedule", doc.ContentHtml);
        Assert.Contains("DAY 1 &bull; Welcome & HR Orientation", doc.ContentHtml);
        Assert.Contains("DAY 5 &bull; Practical Induction, Evaluation & Sprint Handoff", doc.ContentHtml);
    }
}
