namespace NexusAgent.Application.Automation
{
    /// <summary>
    /// Payload data carried by the EmployeeOnboardedEvent.
    /// </summary>
    public sealed record EmployeeOnboardedData(
        string EmployeeName,
        string Department,
        string RoleTitle,
        decimal Salary,
        string Email
    );

    /// <summary>
    /// Fired after a new employee record is persisted to the database.
    /// Triggers the Python automation service to send a welcome email and
    /// optionally post a Slack notification to the #new-hires channel.
    /// </summary>
    public sealed class EmployeeOnboardedEvent : AutomationEventDto
    {
        public EmployeeOnboardedData Data { get; init; } = null!;

        public override object GetData() => Data;
    }
}
