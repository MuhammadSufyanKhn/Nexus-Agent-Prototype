using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Data.Policies;

public interface IPolicyRuleEngine
{
    Task<PolicyDecisionResult> EvaluateOnboardingPolicyAsync(PolicyEvaluationContext context, CancellationToken cancellationToken = default);
    Task<PolicyDecisionResult> EvaluateExpenseComplianceAsync(PolicyEvaluationContext context, CancellationToken cancellationToken = default);
}
