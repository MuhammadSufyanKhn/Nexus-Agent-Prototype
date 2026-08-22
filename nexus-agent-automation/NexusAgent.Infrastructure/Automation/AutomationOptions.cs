namespace NexusAgent.Infrastructure.Automation
{
    /// <summary>
    /// Strongly-typed configuration for the automation service.
    /// Bound from the "Automation" section of appsettings.json / environment variables.
    /// </summary>
    public sealed class AutomationOptions
    {
        /// <summary>
        /// Configuration section name used for binding.
        /// </summary>
        public const string SectionName = "Automation";

        /// <summary>
        /// Identifies which automation provider to use. Currently only "PythonWebhook" is supported.
        /// </summary>
        public string Provider { get; set; } = "PythonWebhook";

        /// <summary>
        /// Full URL of the Python FastAPI webhook endpoint.
        /// Example: http://localhost:8000/webhook
        /// In production: http://python-automation:8000/webhook
        /// </summary>
        public string PythonServiceUrl { get; set; } = "http://localhost:8000/webhook";

        /// <summary>
        /// Shared secret sent as the X-Api-Key header.
        /// MUST be overridden by environment variable Automation__ApiKey in production.
        /// Never commit a real key to source control.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Number of retry attempts (Polly) for transient HTTP failures.
        /// Default is 3 (giving retry delays of 1s, 2s, 4s via exponential backoff).
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// HTTP client timeout in seconds for each individual attempt.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 10;
    }
}
