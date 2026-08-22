namespace NexusAgent.Application.Automation
{
    /// <summary>
    /// Payload for when a budget is successfully allocated to a department.
    /// </summary>
    public sealed record BudgetAllocatedData(
        int DepartmentId,
        decimal Amount,
        decimal NewTotalSpend
    );

    /// <summary>
    /// Payload for when a department's spend exceeds its allocated budget.
    /// </summary>
    public sealed record BudgetExceededData(
        int DepartmentId,
        decimal ExceededAmount,
        decimal CurrentSpend,
        decimal AllocatedBudget
    );

    /// <summary>
    /// Fired when a budget allocation is committed to the database.
    /// Triggers a confirmation notification to the finance team.
    /// </summary>
    public sealed class BudgetAllocatedEvent : AutomationEventDto
    {
        public BudgetAllocatedData Data { get; init; } = null!;

        public override object GetData() => Data;
    }

    /// <summary>
    /// Fired when the system detects that a department's actual spend has
    /// surpassed its allocated budget after a transaction commits.
    /// Triggers an urgent alert email and Slack message to finance/HR.
    /// </summary>
    public sealed class BudgetExceededEvent : AutomationEventDto
    {
        public BudgetExceededData Data { get; init; } = null!;

        public override object GetData() => Data;
    }
}
