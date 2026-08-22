using System.Threading;
using System.Threading.Tasks;
using NexusAgent.Application.Automation;

namespace NexusAgent.Application.Interfaces
{
    /// <summary>
    /// Abstraction for the automation service layer.
    /// The concrete implementation fires-and-forgets a structured event to the external
    /// Python automation microservice via HTTP. Implementations MUST NOT throw exceptions
    /// that bubble up to the calling controller – automation failures are logged but never
    /// roll back the business transaction.
    /// </summary>
    public interface IAutomationService
    {
        /// <summary>
        /// Publishes a structured automation event to the configured downstream service.
        /// Call this only AFTER the database transaction has committed successfully.
        /// </summary>
        /// <param name="eventDto">The strongly-typed event payload.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task PublishAsync(AutomationEventDto eventDto, CancellationToken cancellationToken = default);
    }
}
