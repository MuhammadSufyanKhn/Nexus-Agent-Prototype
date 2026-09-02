using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.DTOs;
using Nexus.Data.Entities;
using Nexus.Data.Enums;

namespace Nexus.Data.Services;

public interface IExpenseService
{
    Task<IEnumerable<ExpenseDto>> GetAllAsync(int? employeeId = null, ExpenseStatus? status = null, string? complianceStatus = null);
    Task<ExpenseDto?> GetByIdAsync(int id);
    Task<ExpenseDto?> GetByClaimNumberAsync(string claimNumber);
    Task<ExpenseDto> CreateAsync(CreateExpenseDto dto);
    Task<ExpenseDto?> UpdateAsync(int id, UpdateExpenseDto dto);
    Task<bool> DeleteAsync(int id);

    // Business & Compliance operations under POL-FIN-002
    Task<ExpenseSweepResultDto> RunComplianceSweepAsync();
    Task<ExpenseDto> ApproveClaimAsync(string claimNumber, decimal? verifyAmount = null, string reviewedBy = "Executive Admin");
    Task<ExpenseDto> RejectClaimAsync(string? claimNumber, decimal? amount, string? category, string reason = "Non-compliant with corporate expense policy", string reviewedBy = "Executive Admin");
    Task<ExpenseDto> FlagClaimAsync(string claimNumber, string flagReason = "Manager Policy Review Required", string reviewedBy = "Executive Admin");
    Task<ExpenseMonthlyAnalyticsDto> GetMonthlyAnalyticsAsync(DateTime? targetDate = null);
    Task<ExpenseCategoryAnalyticsDto> GetCategoryAnalyticsAsync();
    Task<ExpenseVarianceResultDto> GetPolicyVarianceAsync(string category = "Travel");
}

public class ExpenseService : IExpenseService
{
    private readonly NexusDbContext _db;

    public ExpenseService(NexusDbContext db)
    {
        _db = db;
    }

    public static decimal GetPolicyLimit(ExpenseType type, string? category = null)
    {
        var cat = (category ?? type.ToString()).Trim();
        if (cat.Equals("Meal", StringComparison.OrdinalIgnoreCase)) return 50.00m;
        if (cat.Equals("Travel", StringComparison.OrdinalIgnoreCase)) return 250.00m;
        if (cat.Equals("Equipment", StringComparison.OrdinalIgnoreCase)) return 500.00m;
        if (cat.Equals("Software", StringComparison.OrdinalIgnoreCase)) return 500.00m;
        return 100.00m;
    }

    public static ExpenseType ParseCategoryToType(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return ExpenseType.Meal;
        if (category.Contains("travel", StringComparison.OrdinalIgnoreCase) || category.Contains("flight", StringComparison.OrdinalIgnoreCase)) return ExpenseType.Travel;
        if (category.Contains("meal", StringComparison.OrdinalIgnoreCase) || category.Contains("dinner", StringComparison.OrdinalIgnoreCase) || category.Contains("lunch", StringComparison.OrdinalIgnoreCase) || category.Contains("food", StringComparison.OrdinalIgnoreCase)) return ExpenseType.Meal;
        if (category.Contains("equip", StringComparison.OrdinalIgnoreCase) || category.Contains("hardware", StringComparison.OrdinalIgnoreCase) || category.Contains("desk", StringComparison.OrdinalIgnoreCase)) return ExpenseType.Equipment;
        if (category.Contains("soft", StringComparison.OrdinalIgnoreCase) || category.Contains("license", StringComparison.OrdinalIgnoreCase) || category.Contains("tool", StringComparison.OrdinalIgnoreCase)) return ExpenseType.Software;
        if (category.Contains("train", StringComparison.OrdinalIgnoreCase) || category.Contains("course", StringComparison.OrdinalIgnoreCase)) return ExpenseType.Training;
        return ExpenseType.Other;
    }

    private static ExpenseDto MapToDto(Expense e)
    {
        var cat = !string.IsNullOrWhiteSpace(e.Category) ? e.Category : e.ExpenseType.ToString();
        var limit = e.PolicyLimit ?? GetPolicyLimit(e.ExpenseType, cat);
        var variance = e.Variance ?? (e.Amount - limit);
        var compStatus = !string.IsNullOrWhiteSpace(e.ComplianceStatus) 
            ? e.ComplianceStatus 
            : (variance > 0 ? "Flagged" : "Compliant");

        return new ExpenseDto
        {
            Id = e.Id,
            ClaimNumber = !string.IsNullOrWhiteSpace(e.ClaimNumber) ? e.ClaimNumber : $"EXP-{e.Id:D4}",
            EmployeeId = e.EmployeeId,
            EmployeeName = e.Employee?.Name ?? $"Employee #{e.EmployeeId}",
            DepartmentName = e.Employee?.Department?.Name ?? "General",
            ExpenseType = e.ExpenseType,
            Category = cat,
            Amount = e.Amount,
            ExpenseDate = e.ExpenseDate,
            Status = e.Status,
            ComplianceStatus = compStatus,
            PolicyLimit = limit,
            Variance = variance,
            FlagReason = e.FlagReason,
            ReviewedBy = e.ReviewedBy,
            ReviewedDate = e.ReviewedDate,
            Description = e.Description
        };
    }

    public async Task<IEnumerable<ExpenseDto>> GetAllAsync(int? employeeId = null, ExpenseStatus? status = null, string? complianceStatus = null)
    {
        var query = _db.Expenses.Include(e => e.Employee)
                                .ThenInclude(emp => emp!.Department)
                                .AsNoTracking()
                                .AsQueryable();

        if (employeeId.HasValue)
        {
            query = query.Where(e => e.EmployeeId == employeeId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(complianceStatus))
        {
            var filter = complianceStatus.Trim().ToLowerInvariant();
            if (filter.Contains("flag") || filter.Contains("noncompliant") || filter.Contains("violation"))
            {
                query = query.Where(e => e.ComplianceStatus == "Flagged" || e.ComplianceStatus == "NonCompliant" || e.Status == ExpenseStatus.NonCompliant || (e.Variance.HasValue && e.Variance > 0));
            }
            else if (filter.Contains("compliant"))
            {
                query = query.Where(e => e.ComplianceStatus == "Compliant" || e.Status == ExpenseStatus.Compliant || (e.Variance.HasValue && e.Variance <= 0));
            }
        }

        var expenses = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();
        return expenses.Select(MapToDto);
    }

    public async Task<ExpenseDto?> GetByIdAsync(int id)
    {
        var e = await _db.Expenses.Include(exp => exp.Employee)
                                  .ThenInclude(emp => emp!.Department)
                                  .AsNoTracking()
                                  .FirstOrDefaultAsync(exp => exp.Id == id);

        return e == null ? null : MapToDto(e);
    }

    public async Task<ExpenseDto?> GetByClaimNumberAsync(string claimNumber)
    {
        var clean = claimNumber.Trim().TrimStart('#');
        var e = await _db.Expenses.Include(exp => exp.Employee)
                                  .ThenInclude(emp => emp!.Department)
                                  .AsNoTracking()
                                  .FirstOrDefaultAsync(exp => exp.ClaimNumber.ToLower() == clean.ToLower());

        return e == null ? null : MapToDto(e);
    }

    public async Task<ExpenseDto> CreateAsync(CreateExpenseDto dto)
    {
        Employee? employee = null;

        if (dto.EmployeeId > 0)
        {
            employee = await _db.Employees.Include(emp => emp.Department).FirstOrDefaultAsync(emp => emp.Id == dto.EmployeeId);
        }

        if (employee == null && !string.IsNullOrWhiteSpace(dto.EmployeeName))
        {
            var search = dto.EmployeeName.Trim().ToLower();
            employee = await _db.Employees.Include(emp => emp.Department)
                .FirstOrDefaultAsync(emp => emp.Name.ToLower() == search || emp.Name.ToLower().Contains(search));
        }

        if (employee == null)
        {
            var empIdentifier = !string.IsNullOrWhiteSpace(dto.EmployeeName) ? dto.EmployeeName : dto.EmployeeId.ToString();
            throw new ArgumentException($"Employee \"{empIdentifier}\" was not found. The expense claim was not created.");
        }

        var expType = dto.ExpenseType != 0 ? dto.ExpenseType : ParseCategoryToType(dto.Category);
        var categoryName = !string.IsNullOrWhiteSpace(dto.Category) ? dto.Category : expType.ToString();
        var limit = dto.PolicyLimit ?? GetPolicyLimit(expType, categoryName);
        var variance = dto.Variance ?? (dto.Amount - limit);
        var isViolation = variance > 0;
        var compStatus = isViolation ? "Flagged" : "Compliant";
        var flagReason = isViolation 
            ? $"Claim of ${dto.Amount:F2} exceeds allowed {categoryName} policy cap of ${limit:F2} by +${variance:F2}." 
            : null;

        // Generate unique ClaimNumber
        string claimNumber;
        if (!string.IsNullOrWhiteSpace(dto.ClaimNumber))
        {
            claimNumber = dto.ClaimNumber.Trim().TrimStart('#');
        }
        else
        {
            var rnd = new Random();
            int attempt = 0;
            do
            {
                claimNumber = $"EXP-{rnd.Next(1000, 9999)}";
                attempt++;
            } while (attempt < 50 && await _db.Expenses.AnyAsync(e => e.ClaimNumber == claimNumber));
        }

        var expense = new Expense
        {
            ClaimNumber = claimNumber,
            EmployeeId = employee.Id,
            ExpenseType = expType,
            Category = categoryName,
            Amount = dto.Amount,
            ExpenseDate = dto.ExpenseDate,
            Status = ExpenseStatus.Pending,
            ComplianceStatus = compStatus,
            PolicyLimit = limit,
            Variance = variance,
            FlagReason = flagReason,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? $"{categoryName} expense" : dto.Description
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        // Verify save
        var persisted = await _db.Expenses.Include(e => e.Employee).ThenInclude(emp => emp!.Department)
            .FirstOrDefaultAsync(e => e.Id == expense.Id);

        if (persisted == null)
        {
            throw new InvalidOperationException("Database save failed to persist the expense claim record.");
        }

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "EXPENSE_CREATE",
            ToolName = "ExpenseService",
            Target = $"{expense.ClaimNumber} ({employee.Name})",
            Result = $"Claim {expense.ClaimNumber} created for {employee.Name} (${expense.Amount:F2} {categoryName}). Compliance: {compStatus}.",
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return MapToDto(persisted);
    }

    public async Task<ExpenseDto?> UpdateAsync(int id, UpdateExpenseDto dto)
    {
        var expense = await _db.Expenses.Include(e => e.Employee)
                                        .ThenInclude(emp => emp!.Department)
                                        .FirstOrDefaultAsync(e => e.Id == id);
        if (expense == null) return null;

        if (dto.ExpenseType.HasValue) expense.ExpenseType = dto.ExpenseType.Value;
        if (!string.IsNullOrWhiteSpace(dto.Category)) expense.Category = dto.Category;
        if (dto.Amount.HasValue) expense.Amount = dto.Amount.Value;
        if (dto.Status.HasValue) expense.Status = dto.Status.Value;
        if (!string.IsNullOrWhiteSpace(dto.ComplianceStatus)) expense.ComplianceStatus = dto.ComplianceStatus;
        if (dto.PolicyLimit.HasValue) expense.PolicyLimit = dto.PolicyLimit;
        if (dto.Variance.HasValue) expense.Variance = dto.Variance;
        if (!string.IsNullOrWhiteSpace(dto.FlagReason)) expense.FlagReason = dto.FlagReason;
        if (!string.IsNullOrWhiteSpace(dto.ReviewedBy)) expense.ReviewedBy = dto.ReviewedBy;
        if (dto.ReviewedDate.HasValue) expense.ReviewedDate = dto.ReviewedDate;
        if (!string.IsNullOrWhiteSpace(dto.Description)) expense.Description = dto.Description;

        await _db.SaveChangesAsync();
        return MapToDto(expense);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense == null) return false;

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ExpenseSweepResultDto> RunComplianceSweepAsync()
    {
        var claims = await _db.Expenses.Include(e => e.Employee).ToListAsync();
        var flaggedList = new List<FlaggedClaimDto>();
        int compliantCount = 0;
        int flaggedCount = 0;
        decimal totalVariance = 0m;

        foreach (var claim in claims)
        {
            var cat = !string.IsNullOrWhiteSpace(claim.Category) ? claim.Category : claim.ExpenseType.ToString();
            var limit = GetPolicyLimit(claim.ExpenseType, cat);
            var variance = claim.Amount - limit;
            claim.PolicyLimit = limit;
            claim.Variance = variance;

            if (variance > 0)
            {
                claim.ComplianceStatus = "Flagged";
                if (claim.Status == ExpenseStatus.Pending)
                {
                    claim.Status = ExpenseStatus.NonCompliant;
                }
                flaggedCount++;
                totalVariance += variance;
                flaggedList.Add(new FlaggedClaimDto
                {
                    ClaimNumber = claim.ClaimNumber,
                    EmployeeName = claim.Employee?.Name ?? $"Employee #{claim.EmployeeId}",
                    Category = cat,
                    Amount = claim.Amount,
                    PolicyLimit = limit,
                    Variance = variance,
                    FlagReason = $"Claim of ${claim.Amount:F2} exceeds allowed {cat} policy limit of ${limit:F2} by +${variance:F2}."
                });
            }
            else
            {
                claim.ComplianceStatus = "Compliant";
                if (claim.Status == ExpenseStatus.Pending)
                {
                    claim.Status = ExpenseStatus.Compliant;
                }
                compliantCount++;
            }
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "EXPENSE_AI_AUDIT_SWEEP",
            ToolName = "ExpenseService",
            Target = "Expenses Table",
            Result = $"Policy Compliance Sweep completed across {claims.Count} claims: {compliantCount} compliant, {flaggedCount} flagged against POL-FIN-002.",
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Policy Compliance Sweep Completed Successfully against POL-FIN-002.\n");
        sb.AppendLine($"Claims Reviewed: {claims.Count}");
        sb.AppendLine($"Compliant Claims: {compliantCount}");
        sb.AppendLine($"Flagged Claims: {flaggedCount}");
        sb.AppendLine($"Total Policy Variance: ${totalVariance:F2}\n");
        sb.AppendLine("Flagged Claims:");
        foreach (var f in flaggedList)
        {
            sb.AppendLine($"• {f.ClaimNumber} — {f.Category} — ${f.Amount:F2} — Limit ${f.PolicyLimit:F2} — Variance +${f.Variance:F2}");
        }

        return new ExpenseSweepResultDto
        {
            ClaimsReviewed = claims.Count,
            CompliantClaims = compliantCount,
            FlaggedClaimsCount = flaggedCount,
            TotalPolicyVariance = totalVariance,
            FlaggedClaims = flaggedList,
            Summary = sb.ToString().TrimEnd()
        };
    }

    public async Task<ExpenseDto> ApproveClaimAsync(string claimNumber, decimal? verifyAmount = null, string reviewedBy = "Executive Admin")
    {
        var clean = claimNumber.Trim().TrimStart('#');
        var expense = await _db.Expenses.Include(e => e.Employee).ThenInclude(emp => emp!.Department)
            .FirstOrDefaultAsync(e => e.ClaimNumber.ToLower() == clean.ToLower());

        if (expense == null)
        {
            throw new KeyNotFoundException($"Expense claim {clean} was not found. No approval was performed.");
        }

        if (verifyAmount.HasValue && Math.Abs(expense.Amount - verifyAmount.Value) > 0.01m)
        {
            throw new ArgumentException($"Claim amount mismatch for {clean}: system record shows ${expense.Amount:F2}, but request specified ${verifyAmount.Value:F2}. No approval was performed.");
        }

        expense.Status = ExpenseStatus.Approved;
        expense.ReviewedBy = reviewedBy;
        expense.ReviewedDate = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "EXPENSE_APPROVE",
            ToolName = "ExpenseService",
            Target = $"{expense.ClaimNumber} ({expense.Employee?.Name})",
            Result = $"Expense claim {expense.ClaimNumber} for ${expense.Amount:F2} approved by {reviewedBy}.",
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return MapToDto(expense);
    }

    public async Task<ExpenseDto> RejectClaimAsync(string? claimNumber, decimal? amount, string? category, string reason = "Non-compliant with corporate expense policy", string reviewedBy = "Executive Admin")
    {
        Expense? targetClaim = null;

        if (!string.IsNullOrWhiteSpace(claimNumber))
        {
            var clean = claimNumber.Trim().TrimStart('#');
            targetClaim = await _db.Expenses.Include(e => e.Employee).ThenInclude(emp => emp!.Department)
                .FirstOrDefaultAsync(e => e.ClaimNumber.ToLower() == clean.ToLower());

            if (targetClaim == null)
            {
                throw new KeyNotFoundException($"Expense claim {clean} was not found. No rejection was performed.");
            }
        }
        else
        {
            var query = _db.Expenses.Include(e => e.Employee).ThenInclude(emp => emp!.Department)
                .Where(e => e.Status != ExpenseStatus.Rejected);

            if (!string.IsNullOrWhiteSpace(category))
            {
                var catType = ParseCategoryToType(category);
                query = query.Where(e => e.ExpenseType == catType || (e.Category != null && e.Category.ToLower().Contains(category.ToLower())));
            }

            if (amount.HasValue)
            {
                query = query.Where(e => Math.Abs(e.Amount - amount.Value) < 0.01m);
            }

            var matches = await query.ToListAsync();
            if (matches.Count == 0)
            {
                throw new KeyNotFoundException($"No matching non-compliant {category ?? "expense"} claim for ${amount:F2} was found.");
            }
            if (matches.Count > 1)
            {
                var numbers = string.Join(", ", matches.Select(m => m.ClaimNumber));
                throw new InvalidOperationException($"Multiple matching claims found ({numbers}). Please specify the claim number to reject.");
            }

            targetClaim = matches.First();
        }

        targetClaim.Status = ExpenseStatus.Rejected;
        targetClaim.FlagReason = reason;
        targetClaim.ReviewedBy = reviewedBy;
        targetClaim.ReviewedDate = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "EXPENSE_REJECT",
            ToolName = "ExpenseService",
            Target = $"{targetClaim.ClaimNumber} ({targetClaim.Employee?.Name})",
            Result = $"Expense claim {targetClaim.ClaimNumber} for ${targetClaim.Amount:F2} rejected by {reviewedBy}. Reason: {reason}",
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return MapToDto(targetClaim);
    }

    public async Task<ExpenseDto> FlagClaimAsync(string claimNumber, string flagReason = "Manager Policy Review Required", string reviewedBy = "Executive Admin")
    {
        var clean = claimNumber.Trim().TrimStart('#');
        var expense = await _db.Expenses.Include(e => e.Employee).ThenInclude(emp => emp!.Department)
            .FirstOrDefaultAsync(e => e.ClaimNumber.ToLower() == clean.ToLower());

        if (expense == null)
        {
            throw new KeyNotFoundException($"Expense claim {clean} was not found. Cannot flag claim.");
        }

        expense.ComplianceStatus = "Flagged";
        expense.Status = ExpenseStatus.NonCompliant;
        expense.FlagReason = flagReason;
        expense.ReviewedBy = reviewedBy;
        expense.ReviewedDate = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "EXPENSE_FLAG",
            ToolName = "ExpenseService",
            Target = $"{expense.ClaimNumber} ({expense.Employee?.Name})",
            Result = $"Expense claim {expense.ClaimNumber} flagged for policy review. Reason: {flagReason}",
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return MapToDto(expense);
    }

    public async Task<ExpenseMonthlyAnalyticsDto> GetMonthlyAnalyticsAsync(DateTime? targetDate = null)
    {
        var now = targetDate ?? DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

        var monthClaims = await _db.Expenses.Include(e => e.Employee)
            .Where(e => e.ExpenseDate >= startOfMonth && e.ExpenseDate <= endOfMonth)
            .ToListAsync();

        var totalSubmitted = monthClaims.Sum(e => e.Amount);
        var totalCount = monthClaims.Count;
        var pendingAmount = monthClaims.Where(e => e.Status == ExpenseStatus.Pending).Sum(e => e.Amount);
        var approvedAmount = monthClaims.Where(e => e.Status == ExpenseStatus.Approved).Sum(e => e.Amount);
        var flaggedAmount = monthClaims.Where(e => e.Status == ExpenseStatus.NonCompliant || e.ComplianceStatus == "Flagged" || e.ComplianceStatus == "NonCompliant").Sum(e => e.Amount);

        var monthName = now.ToString("MMMM yyyy");

        return new ExpenseMonthlyAnalyticsDto
        {
            Month = monthName,
            TotalSubmitted = totalSubmitted,
            TotalCount = totalCount,
            PendingAmount = pendingAmount,
            ApprovedAmount = approvedAmount,
            FlaggedAmount = flaggedAmount,
            Claims = monthClaims.Select(MapToDto).ToList(),
            Summary = $"Expense Reimbursement Summary — {monthName}\n\nTotal Submitted: ${totalSubmitted:F2}\nTotal Claims: {totalCount}\nPending: ${pendingAmount:F2}\nApproved: ${approvedAmount:F2}\nFlagged for Review: ${flaggedAmount:F2}"
        };
    }

    public async Task<ExpenseCategoryAnalyticsDto> GetCategoryAnalyticsAsync()
    {
        var allClaims = await _db.Expenses.Include(e => e.Employee).ToListAsync();
        var categories = new[] { "Meal", "Travel", "Equipment", "Software" };

        var rows = new List<ExpenseCategoryBreakdownDto>();

        foreach (var cat in categories)
        {
            var catType = ParseCategoryToType(cat);
            var matching = allClaims.Where(e => e.ExpenseType == catType || string.Equals(e.Category, cat, StringComparison.OrdinalIgnoreCase)).ToList();

            var count = matching.Count;
            var totalAmount = matching.Sum(e => e.Amount);
            var compliantCount = matching.Count(e => e.ComplianceStatus == "Compliant" || e.Status == ExpenseStatus.Compliant || (e.Variance.HasValue && e.Variance <= 0));
            var flaggedCount = matching.Count(e => e.ComplianceStatus == "Flagged" || e.ComplianceStatus == "NonCompliant" || e.Status == ExpenseStatus.NonCompliant || (e.Variance.HasValue && e.Variance > 0));
            var approvedCount = matching.Count(e => e.Status == ExpenseStatus.Approved);

            rows.Add(new ExpenseCategoryBreakdownDto
            {
                Category = cat,
                Claims = count,
                TotalAmount = totalAmount,
                Compliant = compliantCount,
                Flagged = flaggedCount,
                Approved = approvedCount
            });
        }

        var totalAmt = allClaims.Sum(e => e.Amount);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Expense Claims Breakdown by Category:\n");
        sb.AppendLine("| Category | Claims | Total Amount | Compliant | Flagged | Approved |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var r in rows)
        {
            sb.AppendLine($"| {r.Category} | {r.Claims} | ${r.TotalAmount:F2} | {r.Compliant} | {r.Flagged} | {r.Approved} |");
        }
        sb.AppendLine($"\nTotal Expenses Claimed Across All Categories: ${totalAmt:F2} ({allClaims.Count} claims)");

        return new ExpenseCategoryAnalyticsDto
        {
            Categories = rows,
            TotalClaims = allClaims.Count,
            TotalAmount = totalAmt,
            Summary = sb.ToString().TrimEnd()
        };
    }

    public async Task<ExpenseVarianceResultDto> GetPolicyVarianceAsync(string category = "Travel")
    {
        var catType = ParseCategoryToType(category);
        var limit = GetPolicyLimit(catType, category);

        var query = _db.Expenses.Include(e => e.Employee)
            .Where(e => (e.ExpenseType == catType || (e.Category != null && e.Category.ToLower() == category.ToLower()))
                     && (e.ComplianceStatus == "Flagged" || e.ComplianceStatus == "NonCompliant" || e.Status == ExpenseStatus.NonCompliant || e.Amount > limit));

        var flaggedClaims = await query.ToListAsync();

        var details = flaggedClaims.Select(c => new FlaggedTravelClaimDetailDto
        {
            ClaimNumber = c.ClaimNumber,
            Employee = c.Employee?.Name ?? $"Employee #{c.EmployeeId}",
            ClaimedAmount = c.Amount,
            PolicyLimit = limit,
            Variance = c.Amount - limit
        }).ToList();

        var totalVariance = details.Sum(d => d.Variance);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Flagged {category} Claims: {flaggedClaims.Count}\n");
        foreach (var vc in details)
        {
            sb.AppendLine($"• {vc.ClaimNumber}\n  Employee: {vc.Employee}\n  Claimed: ${vc.ClaimedAmount:F2}\n  Limit: ${vc.PolicyLimit:F2}\n  Variance: +${vc.Variance:F2}\n");
        }
        sb.AppendLine($"Total Policy Variance Across Flagged {category} Claims: +${totalVariance:F2}");

        return new ExpenseVarianceResultDto
        {
            Category = category,
            PolicyLimit = limit,
            FlaggedCount = flaggedClaims.Count,
            TotalVariance = totalVariance,
            Claims = details,
            Summary = sb.ToString().TrimEnd()
        };
    }
}
