// ============================================================
// CommandExecutionService.cs  –  Integration snippet
// ============================================================
// This file shows how to wire IAutomationService into your
// existing command/service layer.  Copy the relevant sections
// into your real CommandExecutionService (or equivalent).
// ============================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NexusAgent.Application.Automation;
using NexusAgent.Application.Interfaces;

namespace NexusAgent.Application.Commands
{
    /// <summary>
    /// Illustrative command execution service demonstrating the canonical
    /// pattern for firing automation events AFTER a database transaction commits.
    ///
    /// Key rules enforced here:
    ///   1. The automation call is ALWAYS placed AFTER DbContext.SaveChangesAsync().
    ///   2. The automation call is wrapped in try-catch – a failure here
    ///      MUST NOT surface to the caller or roll back any data.
    ///   3. No await on the fire-and-forget path is not acceptable here –
    ///      we DO await, but swallow exceptions.
    /// </summary>
    public sealed class CommandExecutionService
    {
        private readonly IAutomationService _automationService;
        private readonly ILogger<CommandExecutionService> _logger;

        // Inject IAutomationService alongside your DbContext / repository
        public CommandExecutionService(
            IAutomationService automationService,
            ILogger<CommandExecutionService> logger)
        {
            _automationService = automationService;
            _logger = logger;
        }

        // ──────────────────────────────────────────────────────────────
        // Example: Onboard a new employee
        // ──────────────────────────────────────────────────────────────
        public async Task<int> OnboardEmployeeAsync(
            string employeeName,
            string department,
            string roleTitle,
            decimal salary,
            string email,
            string currentUser,
            CancellationToken ct = default)
        {
            // ── 1. Core business logic + DB persist ─────────────────
            // ... your existing EF Core / repository code ...
            // var employee = new Employee { ... };
            // _dbContext.Employees.Add(employee);
            // await _dbContext.SaveChangesAsync(ct);   ← transaction committed here
            // int newEmployeeId = employee.Id;
            int newEmployeeId = 42; // ← placeholder; replace with real ID

            // ── 2. Fire automation event AFTER commit ────────────────
            await PublishSafeAsync(new EmployeeOnboardedEvent
            {
                EventId    = Guid.NewGuid(),
                EventType  = "EmployeeOnboarded",
                EntityId   = newEmployeeId,
                ExecutedBy = currentUser,
                Timestamp  = DateTime.UtcNow,
                Data = new EmployeeOnboardedData(
                    EmployeeName: employeeName,
                    Department:   department,
                    RoleTitle:    roleTitle,
                    Salary:       salary,
                    Email:        email
                )
            }, ct);

            return newEmployeeId;
        }

        // ──────────────────────────────────────────────────────────────
        // Example: Allocate a budget to a department
        // ──────────────────────────────────────────────────────────────
        public async Task AllocateBudgetAsync(
            int departmentId,
            decimal amount,
            decimal newTotalSpend,
            string currentUser,
            CancellationToken ct = default)
        {
            // ... persist budget record ...
            // await _dbContext.SaveChangesAsync(ct);

            await PublishSafeAsync(new BudgetAllocatedEvent
            {
                EventId    = Guid.NewGuid(),
                EventType  = "BudgetAllocated",
                EntityId   = departmentId,
                ExecutedBy = currentUser,
                Timestamp  = DateTime.UtcNow,
                Data = new BudgetAllocatedData(
                    DepartmentId: departmentId,
                    Amount:       amount,
                    NewTotalSpend: newTotalSpend
                )
            }, ct);
        }

        // ──────────────────────────────────────────────────────────────
        // Example: Detect and report budget overspend
        // ──────────────────────────────────────────────────────────────
        public async Task ReportBudgetExceededAsync(
            int departmentId,
            decimal allocatedBudget,
            decimal currentSpend,
            string currentUser,
            CancellationToken ct = default)
        {
            decimal exceededAmount = currentSpend - allocatedBudget;

            // Budget alert is a read-triggered event, not a write – still safe to fire
            await PublishSafeAsync(new BudgetExceededEvent
            {
                EventId    = Guid.NewGuid(),
                EventType  = "BudgetExceeded",
                EntityId   = departmentId,
                ExecutedBy = currentUser,
                Timestamp  = DateTime.UtcNow,
                Data = new BudgetExceededData(
                    DepartmentId:    departmentId,
                    ExceededAmount:  exceededAmount,
                    CurrentSpend:    currentSpend,
                    AllocatedBudget: allocatedBudget
                )
            }, ct);
        }

        // ──────────────────────────────────────────────────────────────
        // Safe wrapper – automation failures NEVER propagate
        // ──────────────────────────────────────────────────────────────
        private async Task PublishSafeAsync(AutomationEventDto evt, CancellationToken ct)
        {
            try
            {
                await _automationService.PublishAsync(evt, ct);
            }
            catch (Exception ex)
            {
                // IAutomationService implementations are already required to swallow
                // exceptions. This outer catch is a last-resort safety net.
                _logger.LogError(
                    ex,
                    "Unexpected exception from IAutomationService for event {EventType} ({EventId}). " +
                    "Business transaction is unaffected.",
                    evt.EventType, evt.EventId);
            }
        }
    }
}
