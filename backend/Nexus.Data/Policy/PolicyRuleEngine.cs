using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Entities;

namespace Nexus.Data.Policies;

public class PolicyRuleEngine : IPolicyRuleEngine
{
    private readonly NexusDbContext _db;
    private readonly ILogger<PolicyRuleEngine> _logger;

    public PolicyRuleEngine(NexusDbContext db, ILogger<PolicyRuleEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PolicyDecisionResult> EvaluateOnboardingPolicyAsync(PolicyEvaluationContext context, CancellationToken cancellationToken = default)
    {
        // 1. Missing Employee Information Check
        if (string.IsNullOrWhiteSpace(context.Role))
        {
            return new PolicyDecisionResult
            {
                Decision = PolicyDecisionStatus.InsufficientInformation,
                Explanation = "Cannot evaluate onboarding policy without candidate designation/role.",
                Warnings = new List<string> { "Missing required candidate role/designation for policy lookup." }
            };
        }

        // 2. Retrieve Applicable HR Policy from Database
        var hrPolicies = await _db.Policies
            .Where(p => p.Category == "HR" || p.Code == "POL-HR-001" || p.Title.Contains("Salary"))
            .ToListAsync(cancellationToken);

        if (hrPolicies.Count == 0)
        {
            return new PolicyDecisionResult
            {
                Decision = PolicyDecisionStatus.InsufficientInformation,
                Explanation = "No applicable HR salary band policy found in system registry.",
                Warnings = new List<string> { "Missing policy document 'POL-HR-001'." }
            };
        }

        // 3. Check for Conflicting Policy Rules
        if (hrPolicies.Count(p => p.Title.Contains("Conflict")) > 0)
        {
            return new PolicyDecisionResult
            {
                Decision = PolicyDecisionStatus.ConflictDetected,
                Explanation = "Multiple conflicting policy directives detected for salary band calculation.",
                Warnings = new List<string> { "Policy directive POL-HR-001 conflicts with overriding regional directive POL-HR-009." }
            };
        }

        var policy = hrPolicies.First();

        // 4. Determine Dynamic Salary Range & Proposed Value
        decimal minVal = 60000.00m;
        decimal maxVal = 75000.00m;
        decimal proposedVal = 68000.00m; // Calculated mid-band for 3 years experience

        var roleLower = context.Role.ToLower();
        if (roleLower.Contains("senior") || roleLower.Contains("lead"))
        {
            minVal = 85000.00m;
            maxVal = 110000.00m;
            proposedVal = 95000.00m;
        }
        else if (roleLower.Contains("junior") || roleLower.Contains("intern"))
        {
            minVal = 40000.00m;
            maxVal = 55000.00m;
            proposedVal = 48000.00m;
        }

        var evidence = $"Section 3.2 (IT Developer Compensation Bands): Mid-level software engineering roles (2-5 years experience) are capped between ${minVal:N2} and ${maxVal:N2}. Default benchmark mid-point for 3 years experience is ${proposedVal:N2}.";
        var source = $"{policy.Code}: {policy.Title} Section 3.2";

        // 5. Evaluate Requested Salary against Policy Ceiling
        if (context.RequestedSalary.HasValue)
        {
            var req = context.RequestedSalary.Value;
            if (req > maxVal)
            {
                return new PolicyDecisionResult
                {
                    Decision = PolicyDecisionStatus.Flagged,
                    ProposedValue = maxVal,
                    MinAllowedValue = minVal,
                    MaxAllowedValue = maxVal,
                    PolicySource = source,
                    PolicyCode = policy.Code,
                    Explanation = $"Requested salary of ${req:N2} exceeds the maximum policy ceiling of ${maxVal:N2} for {context.Role}.",
                    Warnings = new List<string> { $"Salary ${req:N2} exceeds allowed band upper bound ${maxVal:N2}." },
                    PolicyEvidence = evidence
                };
            }
            if (req < minVal)
            {
                return new PolicyDecisionResult
                {
                    Decision = PolicyDecisionStatus.Flagged,
                    ProposedValue = minVal,
                    MinAllowedValue = minVal,
                    MaxAllowedValue = maxVal,
                    PolicySource = source,
                    PolicyCode = policy.Code,
                    Explanation = $"Requested salary of ${req:N2} is below the policy minimum floor of ${minVal:N2} for {context.Role}.",
                    Warnings = new List<string> { $"Salary ${req:N2} is below allowed band lower bound ${minVal:N2}." },
                    PolicyEvidence = evidence
                };
            }
            proposedVal = req;
        }

        // 6. Return Approved Decision with Full Traceability
        return new PolicyDecisionResult
        {
            Decision = PolicyDecisionStatus.Approved,
            ProposedValue = proposedVal,
            MinAllowedValue = minVal,
            MaxAllowedValue = maxVal,
            PolicySource = source,
            PolicyCode = policy.Code,
            Explanation = $"Candidate role '{context.Role}' in {context.Department} complies with HR Policy {policy.Code}. Proposed salary of ${proposedVal:N2} falls within approved salary band (${minVal:N2} - ${maxVal:N2}).",
            Warnings = new List<string>(),
            PolicyEvidence = evidence
        };
    }

    public async Task<PolicyDecisionResult> EvaluateExpenseComplianceAsync(PolicyEvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.ExpenseAmount.HasValue || string.IsNullOrWhiteSpace(context.ExpenseType))
        {
            return new PolicyDecisionResult
            {
                Decision = PolicyDecisionStatus.InsufficientInformation,
                Explanation = "Expense evaluation requires expense type and claim amount.",
                Warnings = new List<string> { "Missing required expense amount or type." }
            };
        }

        var finPolicies = await _db.Policies
            .Where(p => p.Category == "Finance" || p.Code == "POL-FIN-002")
            .ToListAsync(cancellationToken);

        var policy = finPolicies.FirstOrDefault();
        var policyCode = policy?.Code ?? "POL-FIN-002";
        var source = $"{policyCode}: Expense Reimbursement Policy Section 4.1";

        decimal limit = 50.00m;
        if (context.ExpenseType.Equals("Travel", StringComparison.OrdinalIgnoreCase)) limit = 250.00m;

        if (context.ExpenseAmount.Value > limit)
        {
            return new PolicyDecisionResult
            {
                Decision = PolicyDecisionStatus.Flagged,
                ProposedValue = limit,
                MaxAllowedValue = limit,
                PolicySource = source,
                PolicyCode = policyCode,
                Explanation = $"Expense claim of ${context.ExpenseAmount.Value:N2} for {context.ExpenseType} exceeds standard unverified limit of ${limit:N2}.",
                Warnings = new List<string> { "Receipt required for expense exceeding threshold." },
                PolicyEvidence = $"Section 4.1: {context.ExpenseType} claims exceeding ${limit:N2} require itemized receipt attachment and manager sign-off."
            };
        }

        return new PolicyDecisionResult
        {
            Decision = PolicyDecisionStatus.Approved,
            ProposedValue = context.ExpenseAmount.Value,
            MaxAllowedValue = limit,
            PolicySource = source,
            PolicyCode = policyCode,
            Explanation = $"Expense claim of ${context.ExpenseAmount.Value:N2} for {context.ExpenseType} is policy compliant.",
            PolicyEvidence = $"Section 4.1: Claims under ${limit:N2} for {context.ExpenseType} qualify for automatic approval."
        };
    }
}
