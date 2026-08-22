using System;

namespace NexusAgent.Application.Automation
{
    /// <summary>
    /// Base DTO for all automation events published to the Python automation service.
    /// All properties use camelCase JSON serialization to match the Python Pydantic model.
    /// </summary>
    public abstract class AutomationEventDto
    {
        /// <summary>
        /// Unique identifier for this event. Used for idempotency in the Python service.
        /// </summary>
        public Guid EventId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Discriminator string that identifies the event type (e.g., "EmployeeOnboarded").
        /// The Python dispatcher uses this to route to the correct handler.
        /// </summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>
        /// The primary entity ID related to this event (e.g., Employee.Id, DepartmentId).
        /// </summary>
        public int EntityId { get; init; }

        /// <summary>
        /// The user or system principal that triggered this event.
        /// </summary>
        public string ExecutedBy { get; init; } = string.Empty;

        /// <summary>
        /// UTC timestamp of when the event was generated (after DB commit).
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Event-specific payload. Sealed in derived classes as strongly-typed nested objects.
        /// Serialized as an anonymous object so the Python side receives a flat dict.
        /// </summary>
        public abstract object GetData();
    }
}
