// ============================================================
// NexusAgent.Tests/Automation/WebhookAutomationServiceTests.cs
// ============================================================
// Unit tests for WebhookAutomationService.
// Uses Moq to mock IHttpClientFactory and a fake HttpMessageHandler
// to intercept outgoing HTTP calls without a real network.
// ============================================================

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NexusAgent.Application.Automation;
using NexusAgent.Infrastructure.Automation;
using Xunit;

namespace NexusAgent.Tests.Automation
{
    /// <summary>
    /// Fake HttpMessageHandler that returns a configurable response
    /// and records the last request received.
    /// </summary>
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        private int _callCount;

        public HttpRequestMessage? LastRequest { get; private set; }
        public int CallCount => _callCount;

        public FakeHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string body = "{\"status\":\"success\"}")
        {
            _response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(_response);
        }
    }

    /// <summary>
    /// Helper to build a properly configured WebhookAutomationService
    /// with a substituted HttpMessageHandler.
    /// </summary>
    internal static class ServiceFactory
    {
        public static (WebhookAutomationService Service, FakeHttpMessageHandler Handler) Create(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string responseBody = "{\"status\":\"success\"}",
            string? pythonServiceUrl = null,
            string apiKey = "test-key")
        {
            var handler = new FakeHttpMessageHandler(statusCode, responseBody);
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(pythonServiceUrl ?? "http://localhost:8000/webhook")
            };
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory
                .Setup(f => f.CreateClient("AutomationService"))
                .Returns(httpClient);

            var options = Options.Create(new AutomationOptions
            {
                PythonServiceUrl = pythonServiceUrl ?? "http://localhost:8000/webhook",
                ApiKey = apiKey,
                RetryCount = 0, // No retries in unit tests (Polly is not wired here)
                TimeoutSeconds = 5
            });

            var logger = new Mock<ILogger<WebhookAutomationService>>().Object;

            var service = new WebhookAutomationService(mockFactory.Object, options, logger);
            return (service, handler);
        }
    }

    public sealed class WebhookAutomationServiceTests
    {
        // ── Test fixtures ──────────────────────────────────────────────
        private static EmployeeOnboardedEvent BuildEmployeeEvent() => new()
        {
            EventId    = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            EventType  = "EmployeeOnboarded",
            EntityId   = 42,
            ExecutedBy = "admin@nexusagent.io",
            Timestamp  = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc),
            Data = new EmployeeOnboardedData("Alice", "Engineering", "SWE", 100000m, "alice@nexusagent.io")
        };

        // ── Payload serialization ──────────────────────────────────────

        [Fact]
        public async Task PublishAsync_SendsCorrectHttpMethod()
        {
            var (svc, handler) = ServiceFactory.Create();
            await svc.PublishAsync(BuildEmployeeEvent());
            Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        }

        [Fact]
        public async Task PublishAsync_AttachesApiKeyHeader()
        {
            var (svc, handler) = ServiceFactory.Create(apiKey: "secret-test-key");
            await svc.PublishAsync(BuildEmployeeEvent());

            Assert.NotNull(handler.LastRequest);
            Assert.True(
                handler.LastRequest!.Headers.TryGetValues("X-Api-Key", out var values),
                "X-Api-Key header was not sent.");
            Assert.Contains("secret-test-key", values);
        }

        [Fact]
        public async Task PublishAsync_SendsJsonContentType()
        {
            var (svc, handler) = ServiceFactory.Create();
            await svc.PublishAsync(BuildEmployeeEvent());
            Assert.Equal("application/json", handler.LastRequest?.Content?.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task PublishAsync_PayloadContainsCorrectEventType()
        {
            var (svc, handler) = ServiceFactory.Create();
            await svc.PublishAsync(BuildEmployeeEvent());

            var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
            Assert.Contains("\"eventType\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("EmployeeOnboarded", body);
        }

        // ── Failure resilience ─────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_WhenServiceReturns500_DoesNotThrow()
        {
            var (svc, handler) = ServiceFactory.Create(HttpStatusCode.InternalServerError, "{\"status\":\"failed\"}");
            // Must not throw – business transaction must be unaffected
            var exception = await Record.ExceptionAsync(() => svc.PublishAsync(BuildEmployeeEvent()));
            Assert.Null(exception);
        }

        [Fact]
        public async Task PublishAsync_WhenNetworkFails_DoesNotThrow()
        {
            // Use a handler that throws to simulate network failure
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Throws(new HttpRequestException("Network unreachable"));

            var options = Options.Create(new AutomationOptions { ApiKey = "key", PythonServiceUrl = "http://localhost:8000/webhook" });
            var logger = new Mock<ILogger<WebhookAutomationService>>().Object;
            var svc = new WebhookAutomationService(mockFactory.Object, options, logger);

            var exception = await Record.ExceptionAsync(() => svc.PublishAsync(BuildEmployeeEvent()));
            Assert.Null(exception);
        }

        [Fact]
        public async Task PublishAsync_NullEventDto_DoesNotThrow()
        {
            var (svc, _) = ServiceFactory.Create();
            var exception = await Record.ExceptionAsync(() => svc.PublishAsync(null!));
            Assert.Null(exception);
        }

        // ── BudgetExceeded event ───────────────────────────────────────

        [Fact]
        public async Task PublishAsync_BudgetExceededEvent_CorrectPayload()
        {
            var (svc, handler) = ServiceFactory.Create();

            await svc.PublishAsync(new BudgetExceededEvent
            {
                EventId    = Guid.NewGuid(),
                EventType  = "BudgetExceeded",
                EntityId   = 7,
                ExecutedBy = "system",
                Timestamp  = DateTime.UtcNow,
                Data = new BudgetExceededData(7, 5000m, 105000m, 100000m)
            });

            var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
            Assert.Contains("BudgetExceeded", body);
        }
    }
}
