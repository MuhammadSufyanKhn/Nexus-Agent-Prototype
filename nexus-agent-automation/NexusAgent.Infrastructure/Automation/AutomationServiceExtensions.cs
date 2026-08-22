using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusAgent.Application.Interfaces;
using NexusAgent.Infrastructure.Automation;
using Polly;
using Polly.Extensions.Http;

namespace NexusAgent.Infrastructure.Extensions
{
    /// <summary>
    /// DI registration extension for the Nexus-Agent automation layer.
    ///
    /// Add to Program.cs / Startup.cs:
    ///   builder.Services.AddAutomation(builder.Configuration);
    ///
    /// Registers:
    ///   • AutomationOptions (IOptions&lt;AutomationOptions&gt;)
    ///   • Named HttpClient "AutomationService" with:
    ///       - BaseAddress  → Automation:PythonServiceUrl
    ///       - X-Api-Key    → Automation:ApiKey
    ///       - Timeout      → Automation:TimeoutSeconds
    ///       - Polly retry  → exponential back-off (1s, 2s, 4s …)
    ///   • IAutomationService → WebhookAutomationService (Singleton)
    /// </summary>
    public static class AutomationServiceExtensions
    {
        public static IServiceCollection AddAutomation(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // ── 1. Bind strongly-typed options ──────────────────────
            var section = configuration.GetSection(AutomationOptions.SectionName);
            services.Configure<AutomationOptions>(section);

            var options = section.Get<AutomationOptions>() ?? new AutomationOptions();

            // ── 2. Validate required configuration ──────────────────
            if (string.IsNullOrWhiteSpace(options.PythonServiceUrl))
                throw new InvalidOperationException(
                    "Automation:PythonServiceUrl is not configured. " +
                    "Add it to appsettings.json or as an environment variable.");

            if (string.IsNullOrWhiteSpace(options.ApiKey))
                throw new InvalidOperationException(
                    "Automation:ApiKey is not configured. " +
                    "Set the environment variable Automation__ApiKey in production.");

            // ── 3. Polly retry policy: exponential back-off ─────────
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()              // 5xx + network errors
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    retryCount: options.RetryCount,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                    // 1s, 2s, 4s …
                    onRetry: (outcome, timespan, attempt, ctx) =>
                    {
                        // ILogger is not available at policy-build time; use LoggerFactory
                        // The service itself also logs at each retry level.
                        Console.Error.WriteLine(
                            $"[AutomationRetry] Attempt {attempt} failed " +
                            $"({outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}). " +
                            $"Waiting {timespan.TotalSeconds:F1}s before next retry.");
                    });

            // ── 4. Named HttpClient ─────────────────────────────────
            services
                .AddHttpClient("AutomationService", client =>
                {
                    client.BaseAddress = new Uri(options.PythonServiceUrl);
                    client.Timeout     = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                })
                .AddPolicyHandler(retryPolicy);

            // ── 5. Register the service ─────────────────────────────
            services.AddSingleton<IAutomationService, WebhookAutomationService>();

            return services;
        }
    }
}
