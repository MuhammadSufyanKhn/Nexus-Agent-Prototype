using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Nexus.Data.Enums;

namespace Nexus.Data.DTOs;

public class ExpenseDto
{
    public int Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public ExpenseType ExpenseType { get; set; }
    public string ExpenseTypeName => ExpenseType.ToString();
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public DateTime SubmittedDate => ExpenseDate;
    public ExpenseStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string ComplianceStatus { get; set; } = "Pending";
    public decimal? PolicyLimit { get; set; }
    public decimal? Variance { get; set; }
    public string? FlagReason { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CreateExpenseDto
{
    public int EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? ClaimNumber { get; set; }
    public ExpenseType ExpenseType { get; set; } = ExpenseType.Meal;
    public string? Category { get; set; }

    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public string? ComplianceStatus { get; set; }
    public decimal? PolicyLimit { get; set; }
    public decimal? Variance { get; set; }
    public string? FlagReason { get; set; }
}

public class UpdateExpenseDto
{
    public ExpenseType? ExpenseType { get; set; }
    public string? Category { get; set; }
    public decimal? Amount { get; set; }
    public ExpenseStatus? Status { get; set; }
    public string? ComplianceStatus { get; set; }
    public decimal? PolicyLimit { get; set; }
    public decimal? Variance { get; set; }
    public string? FlagReason { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? Description { get; set; }
}

public class FlaggedClaimDto
{
    public string ClaimNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PolicyLimit { get; set; }
    public decimal Variance { get; set; }
    public string FlagReason { get; set; } = string.Empty;
}

public class ExpenseSweepResultDto
{
    public int ClaimsReviewed { get; set; }
    public int CompliantClaims { get; set; }
    public int FlaggedClaimsCount { get; set; }
    public decimal TotalPolicyVariance { get; set; }
    public List<FlaggedClaimDto> FlaggedClaims { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class ExpenseMonthlyAnalyticsDto
{
    public string Month { get; set; } = string.Empty;
    public decimal TotalSubmitted { get; set; }
    public int TotalCount { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal FlaggedAmount { get; set; }
    public List<ExpenseDto> Claims { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class ExpenseCategoryBreakdownDto
{
    public string Category { get; set; } = string.Empty;
    public int Claims { get; set; }
    public decimal TotalAmount { get; set; }
    public int Compliant { get; set; }
    public int Flagged { get; set; }
    public int Approved { get; set; }
}

public class ExpenseCategoryAnalyticsDto
{
    public List<ExpenseCategoryBreakdownDto> Categories { get; set; } = new();
    public int TotalClaims { get; set; }
    public decimal TotalAmount { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class FlaggedTravelClaimDetailDto
{
    public string ClaimNumber { get; set; } = string.Empty;
    public string Employee { get; set; } = string.Empty;
    public decimal ClaimedAmount { get; set; }
    public decimal PolicyLimit { get; set; }
    public decimal Variance { get; set; }
}

public class ExpenseVarianceResultDto
{
    public string Category { get; set; } = string.Empty;
    public decimal PolicyLimit { get; set; }
    public int FlaggedCount { get; set; }
    public decimal TotalVariance { get; set; }
    public List<FlaggedTravelClaimDetailDto> Claims { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}
