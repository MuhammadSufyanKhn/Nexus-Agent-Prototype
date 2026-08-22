using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexusAgent.Application.Automation;
using NexusAgent.Application.Interfaces;

namespace NexusAgent.Infrastructure.Automation
{
    /// <summary>
    /// Sends automation events to the external Python FastAPI microservice via HTTP POST.
    ///
    /// Resilience contract:
    ///   • Retries up to AutomationOptions.RetryCount times with exponential backoff
    ///     (delegated to Polly, registered in DI via AddAutomation()).
    ///   • NEVER throws – all failures are caught and logged so the calling business
    ///     transaction is not rolled back due to an automation side-effect.
    ///
    /// Security:
    ///   • Attaches the shared X-Api-Key header on every request.
    ///   • HttpClient is injected by the factory (named client "AutomationService")
    ///     with BaseAddress and default headers pre-configured.
    /// </summary>
    public sealed class WebhookAutomationService : IAutomationService
    {
        // ──────────────────────────────────────────────────────────────
        // Named HttpClient registered in AddAutomation() extension
        // ──────────────────────────────────────────────────────────────
        private const string HttpClientName = "AutomationService";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AutomationOptions _options;
        private readonly ILogger<WebhookAutomationService> _logger;

        public WebhookAutomationService(
            IHttpClientFactory httpClientFactory,
            IOptions<AutomationOptions> options,
            ILogger<WebhookAutomationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task PublishAsync(AutomationEventDto eventDto, CancellationToken cancellationToken = default)
        {
            if (eventDto is null)
            {
                _logger.LogWarning("PublishAsync called with null eventDto – skipping.");
                return;
            }

            // Build the wire payload expected by the Python Pydantic model
            var payload = new AutomationWirePayload
            {
                EventId    = eventDto.EventId.ToString(),
                EventType  = eventDto.EventType,
                EntityId   = eventDto.EntityId,
                Data       = eventDto.GetData(),
                ExecutedBy = eventDto.ExecutedBy,
                Timestamp  = eventDto.Timestamp
            };

            _logger.LogInformation(
                "Publishing automation event {EventType} (id={EventId}) for entity {EntityId}.",
                payload.EventType, payload.EventId, payload.EntityId);

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);

                // Polly policy is already registered on the named HttpClient handler chain.
                // We still wrap in try-catch to ensure NO exception escapes this method.
                var response = await client.PostAsJsonAsync(
                    string.Empty, // BaseAddress already points at /webhook
                    payload,
                    SerializerOptions,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation(
                        "Automation event {EventType} (id={EventId}) delivered. Response: {Body}",
                        payload.EventType, payload.EventId, body);
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Automation event {EventType} (id={EventId}) rejected by Python service. " +
                        "HTTP {StatusCode}: {Body}",
                        payload.EventType, payload.EventId, (int)response.StatusCode, body);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout from the HttpClient – log and swallow.
                _logger.LogError(
                    "Automation event {EventType} (id={EventId}) timed out after all retry attempts.",
                    payload.EventType, payload.EventId);
            }
            catch (Exception ex)
            {
                // All other transient / Polly-exhausted failures – never propagate.
                _logger.LogError(
                    ex,
                    "Automation event {EventType} (id={EventId}) failed permanently after retries. " +
                    "The business transaction was NOT affected.",
                    payload.EventType, payload.EventId);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Private wire DTO – matches Python AutomationEvent Pydantic model
        // ──────────────────────────────────────────────────────────────
        private sealed class AutomationWirePayload
        {
            public string   EventId    { get; init; } = string.Empty;
            public string   EventType  { get; init; } = string.Empty;
            public int      EntityId   { get; init; }
            public object   Data       { get; init; } = new();
            public string   ExecutedBy { get; init; } = string.Empty;
            public DateTime Timestamp  { get; init; }
        }
    }
}
