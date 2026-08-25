using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Sends team notifications via Slack Webhook (or logs as mock if no Webhook URL set).
/// Supports broadcasting leave, onboarding, transfer, and emergency alerts.
/// </summary>
public class SlackNotifyTool : IAgentTool
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<SlackNotifyTool> _logger;

    public SlackNotifyTool(HttpClient httpClient, IConfiguration config, ILogger<SlackNotifyTool> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "slack.notify",
        Description = "Send notification to a team or channel on Slack (e.g., announce sick day, team update, transfer).",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""channel"":          { ""type"": ""string"" },
            ""team"":             { ""type"": ""string"" },
            ""employeeName"":     { ""type"": ""string"" },
            ""message"":          { ""type"": ""string"" },
            ""notificationType"": { ""type"": ""string"" }
          },
          ""required"": [""message""]
        }",
        RequiredPermission = "system.execution",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var msg = context.GetArgument<string>("message");
        if (string.IsNullOrWhiteSpace(msg))
            return Task.FromResult(ValidationResult.Failure("Notification message cannot be empty."));
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var validation = await ValidateInputAsync(context);
        if (!validation.IsValid)
            return ToolExecutionResult.Failure(string.Join("; ", validation.Errors), Definition.RiskLevel);

        var sw = Stopwatch.StartNew();
        try
        {
            var channel  = context.GetArgument<string>("channel") ?? "#general";
            var team     = context.GetArgument<string>("team") ?? "Team";
            var empName  = context.GetArgument<string>("employeeName") ?? "";
            var msg      = context.GetArgument<string>("message")!;
            var typeStr  = context.GetArgument<string>("notificationType") ?? "INFO";

            var webhookUrl = _config["Slack:WebhookUrl"];
            bool sentRealNotification = false;

            var fullText = $"[{typeStr}] *{team} Announcement*:\n{msg}";
            if (!string.IsNullOrWhiteSpace(empName))
                fullText = $"[{typeStr}] *{team} Alert*: {empName}\n{msg}";

            if (!string.IsNullOrWhiteSpace(webhookUrl) && Uri.IsWellFormedUriString(webhookUrl, UriKind.Absolute))
            {
                var payload = JsonSerializer.Serialize(new { channel, text = fullText });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(webhookUrl, content);
                sentRealNotification = response.IsSuccessStatusCode;
            }

            sw.Stop();

            _logger.LogInformation("Slack notification sent to {Channel}: {Message}", channel, fullText);

            return ToolExecutionResult.Success(new
            {
                message          = $"Slack notification dispatched to {channel} ({team}).",
                channel,
                team,
                textSent         = fullText,
                sentRealWebhook  = sentRealNotification,
                mode             = sentRealNotification ? "LIVE_WEBHOOK" : "MOCK_BROADCAST"
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SlackNotifyTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
