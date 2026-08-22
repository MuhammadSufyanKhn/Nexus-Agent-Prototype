using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Data.Policies;

public interface IPolicyComplianceEngine
{
    Task<ComplianceResult> EvaluateExpenseComplianceAsync(string? employeeName = null, int? employeeId = null, int? expenseId = null, CancellationToken cancellationToken = default);
}
