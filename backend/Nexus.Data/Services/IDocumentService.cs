using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nexus.Data.Entities;

namespace Nexus.Data.Services;

public interface IDocumentService
{
    Task<GeneratedDocument> GenerateOnboardingPackageAsync(string employeeName, string departmentName, string designation, decimal salary, DateTime? startDate, Guid? runId = null);
    Task<GeneratedDocument> GenerateOffboardingPackageAsync(string employeeName, string departmentName, string designation, DateTime? effectiveDate, Guid? runId = null);
    Task<GeneratedDocument> GenerateOrientationScheduleAsync(List<string> internNames, string departmentName, DateTime? startDate, Guid? runId = null);
    Task<GeneratedDocument> GenerateSummerInternInductionScheduleAsync(string departmentName, string targetAudience, int durationDays = 5, Guid? runId = null);
    Task<GeneratedDocument> GenerateExpenseReportAsync(List<Expense> expenses, decimal totalClaimed, decimal totalApproved, decimal totalFlagged, Guid? runId = null);
    Task<GeneratedDocument> GenerateCorporateExpenseAuditReportAsync(List<Expense> expenses, List<Policy> policies, Guid? runId = null);
    Task<GeneratedDocument> GenerateComprehensiveHrReportAsync(
        List<Employee> employees,
        List<Department> departments,
        List<JobOpening> jobOpenings,
        List<CandidateApplication> candidates,
        List<Expense> expenses,
        List<Budget> budgets,
        MasterBudget? masterBudget,
        List<Policy> policies,
        List<OnboardingTask> onboardingTasks,
        Guid? runId = null);
    Task<GeneratedDocument?> GetDocumentAsync(string documentId);
    Task<byte[]?> GetDocumentFileAsync(string documentId);
}

