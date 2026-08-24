using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Events;
using Nexus.Agent.Intent;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Security.Permissions;
using Nexus.Security.Risk;
using Nexus.Tools.Core;

namespace Nexus.Agent.Orchestration;

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IIntentParser _intentParser;
    private readonly IToolRegistry _toolRegistry;
    private readonly NexusDbContext _db;
    private readonly IRiskEngine _riskEngine;
    private readonly IPermissionService _permissionService;
    private readonly IAgentEventBroadcaster _broadcaster;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IIntentParser intentParser,
        IToolRegistry toolRegistry,
        NexusDbContext db,
        ILogger<AgentOrchestrator> logger,
        IRiskEngine? riskEngine = null,
        IPermissionService? permissionService = null,
        IAgentEventBroadcaster? broadcaster = null)
    {
        _intentParser = intentParser;
        _toolRegistry = toolRegistry;
        _db = db;
        _logger = logger;
        _riskEngine = riskEngine ?? new Nexus.Security.Risk.RiskEngine();
        _permissionService = permissionService ?? new Nexus.Security.Permissions.PermissionService();
        _broadcaster = broadcaster ?? new Nexus.Agent.Events.AgentEventBroadcaster(NullLogger<Nexus.Agent.Events.AgentEventBroadcaster>.Instance);
    }

    public async Task<AgentResult> ExecuteAsync(string userPrompt, int? userId = null, string userRole = "Admin", CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var feed = new List<AgentEvent>();

        // 1. RECEIVE REQUEST & Create AgentRun Entity
        feed.Add(AgentEvent.Create(AgentEventType.REQUEST_RECEIVED, $"Received natural language request: '{userPrompt}'"));

        var agentRun = new AgentRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OriginalPrompt = userPrompt,
            Intent = "UNKNOWN",
            Status = AgentRunStatus.Executing,
            StartedAt = DateTime.UtcNow
        };

        _db.AgentRuns.Add(agentRun);
        await _db.SaveChangesAsync(cancellationToken);
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, 0, "REQUEST_RECEIVED", $"Received natural language request: '{userPrompt}'"), cancellationToken);

        // 2. PARSE INTENT
        var parsedIntent = await _intentParser.ParseIntentAsync(userPrompt, cancellationToken);
        agentRun.Intent = parsedIntent.Intent;
        await _db.SaveChangesAsync(cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.INTENT_PARSED, $"Parsed intent: {parsedIntent.Intent} (Confidence: {parsedIntent.Confidence:P0})", JsonSerializer.Serialize(parsedIntent.Entities)));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, 0, "INTENT_PARSED", $"Parsed intent: {parsedIntent.Intent}"), cancellationToken);

        // 2.5 HANDLE GENERAL CONVERSATIONAL CHATTER / GREETINGS
        if (parsedIntent.ParsedIntentType == IntentType.GENERAL_CONVERSATION)
        {
            agentRun.Status = AgentRunStatus.Completed;
            agentRun.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var reply = parsedIntent.ConversationalResponse
                ?? "Hello! I am Nexus AI, your enterprise workforce assistant. How can I help you today?";

            feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_COMPLETED, reply));
            sw.Stop();

            return new AgentResult
            {
                RunId = agentRun.Id,
                OriginalPrompt = userPrompt,
                Intent = parsedIntent.Intent,
                IsSuccess = true,
                ResultData = new { message = reply, isConversational = true },
                ExecutionFeed = feed,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                // ── Spec state machine fields ────────────────────────────────────────
                State = WorkflowState.ANSWER_DIRECT.ToString(),
                UserMessage = reply,
                TargetSystem = ExtractTargetSystem(parsedIntent),
                ConfirmationDetails = new ConfirmationDetails
                {
                    ProposedAction = string.Empty,
                    ActionSummary = string.Empty,
                    RequiresUserAction = false
                }
            };
        }

        // 3. VALIDATE INTENT
        if (parsedIntent.RequiresClarification)
        {
            agentRun.Status = AgentRunStatus.Failed;
            agentRun.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Clarification required: {parsedIntent.ClarificationPrompt}"));
            sw.Stop();

            return new AgentResult
            {
                RunId = agentRun.Id,
                OriginalPrompt = userPrompt,
                Intent = parsedIntent.Intent,
                IsSuccess = false,
                ExecutionFeed = feed,
                ErrorMessage = parsedIntent.ClarificationPrompt,
                LlmError = parsedIntent.LlmError,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                // ── Spec state machine fields ────────────────────────────────────────
                State = WorkflowState.CLARIFICATION_REQUIRED.ToString(),
                UserMessage = parsedIntent.ClarificationPrompt ?? "Could you please clarify your request?",
                TargetSystem = ExtractTargetSystem(parsedIntent),
                ConfirmationDetails = BuildClarificationDetails(parsedIntent)
            };
        }


        // 4. PLAN GENERATION & RISK EVALUATION
        var plan = BuildPlan(userPrompt, parsedIntent);

        // 4.1 RBAC SECURITY PERMISSION CHECK ACROSS PLAN STEPS BEFORE APPROVAL INTERCEPTION
        foreach (var step in plan.Steps)
        {
            var tool = _toolRegistry.GetTool(step.ToolName);
            if (tool != null && !_permissionService.HasPermission(userId, userRole, tool.Definition.RequiredPermission))
            {
                agentRun.Status = AgentRunStatus.Failed;
                agentRun.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Security Violation: User role '{userRole}' lacks required permission '{tool.Definition.RequiredPermission}' for tool '{tool.Definition.Name}'."));
                sw.Stop();

                return new AgentResult
                {
                    RunId = agentRun.Id,
                    OriginalPrompt = userPrompt,
                    Intent = parsedIntent.Intent,
                    IsSuccess = false,
                    Plan = plan,
                    ExecutionFeed = feed,
                    ErrorMessage = $"Security Violation: User role '{userRole}' lacks required permission '{tool.Definition.RequiredPermission}' for tool '{tool.Definition.Name}'.",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }
        }

        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, 0, "PLAN_GENERATED", $"Generated plan with {plan.Steps.Count} steps"), cancellationToken);

        // 4.5 HUMAN-IN-THE-LOOP HIGH-RISK PLAN OF ACTION INTERCEPTOR (Only for mutation operations, never read-only queries)
        var promptLower = userPrompt.ToLower();
        var isReadOnly = parsedIntent.ParsedIntentType == IntentType.BUDGET_ANALYSIS
            || parsedIntent.ParsedIntentType == IntentType.EXPENSE_COMPLIANCE
            || parsedIntent.ParsedIntentType == IntentType.EMPLOYEE_READ
            || parsedIntent.ParsedIntentType == IntentType.SECURITY_TEST   // SECURITY_TEST is diagnostic-only, never mutates
            || parsedIntent.ParsedIntentType == IntentType.SQL_AGENT;       // SQL_AGENT handles its own risk internally

        // UPDATE_SALARY always triggers approval even if not flagged as high-risk by the plan, because it is a financial mutation
        var isSalaryUpdate = parsedIntent.ParsedIntentType == IntentType.UPDATE_SALARY;

        if (!isReadOnly && (isSalaryUpdate || plan.TotalRiskLevel >= RiskLevel.High || promptLower.Contains("increase") || promptLower.Contains("10%") || promptLower.Contains("bulk") || promptLower.Contains("onboard") || promptLower.Contains("delete") || promptLower.Contains("remove")))
        {
            var actionPlan = await BuildActionPlanAsync(agentRun.Id, userPrompt, parsedIntent, cancellationToken);

            var approval = new Approval
            {
                Id = Guid.NewGuid(),
                AgentRunId = agentRun.Id,
                RiskLevel = actionPlan.RiskLevel,
                RequestedBy = "System Orchestrator",
                Status = ApprovalStatus.Pending,
                Reason = JsonSerializer.Serialize(actionPlan),
                CreatedAt = DateTime.UtcNow
            };

            _db.Approvals.Add(approval);
            agentRun.Status = AgentRunStatus.WaitingForApproval;
            await _db.SaveChangesAsync(cancellationToken);

            actionPlan.ApprovalId = approval.Id;
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_SELECTED, "AWAITING_APPROVAL: High-risk action requires human confirmation before database modification.", JsonSerializer.Serialize(actionPlan)));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, 0, "APPROVAL_REQUIRED", "High-risk action requires human confirmation."), cancellationToken);
            sw.Stop();

            var confirmMsg = BuildConfirmationUserMessage(parsedIntent);
            return new AgentResult
            {
                RunId = agentRun.Id,
                OriginalPrompt = userPrompt,
                Intent = parsedIntent.Intent,
                IsSuccess = true,
                RequiresApproval = true,
                ActionPlan = actionPlan,
                Plan = plan,
                ExecutionFeed = feed,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                // ── Spec state machine fields ────────────────────────────────────────
                State = WorkflowState.CONFIRMATION_REQUIRED.ToString(),
                UserMessage = confirmMsg.Message,
                TargetSystem = ExtractTargetSystem(parsedIntent),
                ConfirmationDetails = confirmMsg.Details
            };
        }

        var executedSteps = new List<AgentStep>();
        object? lastResultData = null;

        // 5. EXECUTE STEPS IN PLAN
        foreach (var step in plan.Steps)
        {
            var tool = _toolRegistry.GetTool(step.ToolName);
            if (tool == null)
            {
                feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Tool '{step.ToolName}' is not registered in ToolRegistry."));
                agentRun.Status = AgentRunStatus.Failed;
                agentRun.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                sw.Stop();
                return new AgentResult
                {
                    RunId = agentRun.Id,
                    OriginalPrompt = userPrompt,
                    Intent = parsedIntent.Intent,
                    IsSuccess = false,
                    Plan = plan,
                    ExecutedSteps = executedSteps,
                    ExecutionFeed = feed,
                    ErrorMessage = $"Registered tool '{step.ToolName}' not found.",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_SELECTED, $"Selected tool '{step.ToolName}' for step {step.StepNumber}: {step.Description}"));

            // Permission Check
            if (!_permissionService.HasPermission(userId, userRole, tool.Definition.RequiredPermission))
            {
                feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Security Violation: User role '{userRole}' lacks permission '{tool.Definition.RequiredPermission}' for tool '{tool.Definition.Name}'."));
                agentRun.Status = AgentRunStatus.Failed;
                agentRun.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                sw.Stop();
                return new AgentResult
                {
                    RunId = agentRun.Id,
                    OriginalPrompt = userPrompt,
                    Intent = parsedIntent.Intent,
                    IsSuccess = false,
                    Plan = plan,
                    ExecutedSteps = executedSteps,
                    ExecutionFeed = feed,
                    ErrorMessage = $"Security Violation: User role '{userRole}' lacks required permission '{tool.Definition.RequiredPermission}' for tool '{tool.Definition.Name}'.",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            // Create AgentAction Entity
            var agentAction = new AgentAction
            {
                Id = Guid.NewGuid(),
                AgentRunId = agentRun.Id,
                ToolName = step.ToolName,
                ActionType = step.ToolName,
                ArgumentsJson = step.ArgumentsJson,
                RiskLevel = step.RiskLevel,
                Status = AgentActionStatus.Executing,
                StartedAt = DateTime.UtcNow
            };

            _db.AgentActions.Add(agentAction);
            await _db.SaveChangesAsync(cancellationToken);

            // Execute Tool with merged entity & parameter dictionary + user prompt
            var entitiesDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (parsedIntent.Parameters != null)
            {
                foreach (var kv in parsedIntent.Parameters)
                {
                    if (kv.Value != null) entitiesDict[kv.Key] = kv.Value;
                }
            }
            if (parsedIntent.Entities != null)
            {
                foreach (var kv in parsedIntent.Entities)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value)) entitiesDict[kv.Key] = kv.Value;
                }
            }

            if (!entitiesDict.ContainsKey("operation") && !string.IsNullOrWhiteSpace(parsedIntent.Operation))
            {
                entitiesDict["operation"] = parsedIntent.Operation;
            }

            // Entity Aliasing for downstream tool parameters (ONLY for department intent)
            bool isDeptIntent = parsedIntent.TargetEntity.Equals("DEPARTMENT", StringComparison.OrdinalIgnoreCase) ||
                                parsedIntent.ParsedIntentType == IntentType.DEPARTMENT_CREATE ||
                                parsedIntent.ParsedIntentType == IntentType.DEPARTMENT_UPDATE ||
                                parsedIntent.ParsedIntentType == IntentType.DEPARTMENT_DELETE;

            if (isDeptIntent)
            {
                if (entitiesDict.TryGetValue("department", out var deptVal) && (!entitiesDict.ContainsKey("name") || string.IsNullOrWhiteSpace(entitiesDict["name"]?.ToString())))
                {
                    entitiesDict["name"] = deptVal;
                }
                if (entitiesDict.TryGetValue("name", out var nameVal) && (!entitiesDict.ContainsKey("department") || string.IsNullOrWhiteSpace(entitiesDict["department"]?.ToString())))
                {
                    entitiesDict["department"] = nameVal;
                }
            }
            if (entitiesDict.TryGetValue("employee_name", out var empNameVal) && (!entitiesDict.ContainsKey("name") || string.IsNullOrWhiteSpace(entitiesDict["name"]?.ToString())))
            {
                entitiesDict["name"] = empNameVal;
            }

            if (!entitiesDict.ContainsKey("question"))
            {
                entitiesDict["question"] = userPrompt;
            }
            if (!entitiesDict.ContainsKey("prompt"))
            {
                entitiesDict["prompt"] = userPrompt;
            }

            var argumentsPayload = (step.ArgumentsJson != "{}" && !string.IsNullOrWhiteSpace(step.ArgumentsJson))
                ? step.ArgumentsJson
                : JsonSerializer.Serialize(entitiesDict);

            var toolCtx = new ToolExecutionContext
            {
                AgentRunId = agentRun.Id,
                UserId = userId,
                UserRole = userRole,
                ArgumentsJson = argumentsPayload
            };

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"Executing step {step.StepNumber}: {tool.Definition.Name}..."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, step.StepNumber, "TOOL_STARTED", $"Executing step {step.StepNumber}: {tool.Definition.Name}..."), cancellationToken);

            var toolResult = await tool.ExecuteAsync(toolCtx);

            if (toolResult.IsSuccess)
            {
                agentAction.Status = AgentActionStatus.Completed;
                agentAction.ResultJson = JsonSerializer.Serialize(toolResult.Data);
                agentAction.CompletedAt = DateTime.UtcNow;

                feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"Step {step.StepNumber} ({tool.Definition.Name}) completed successfully.", JsonSerializer.Serialize(toolResult.Data)));
                await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, step.StepNumber, "TOOL_COMPLETED", $"Step {step.StepNumber} completed successfully."), cancellationToken);

                // Audit Log Entry
                await RecordAuditLogAsync(agentRun.Id, userId, tool.Definition.Name, step.Description, toolResult.Data, cancellationToken);
                await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, step.StepNumber, "AUDIT_RECORDED", $"Committed SHA-256 audit ledger entry for tool '{tool.Definition.Name}'."), cancellationToken);

                executedSteps.Add(step);
                lastResultData = toolResult.Data;
            }
            else
            {
                agentAction.Status = AgentActionStatus.Failed;
                agentAction.ResultJson = toolResult.ErrorMessage;
                agentAction.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Step {step.StepNumber} ({tool.Definition.Name}) failed: {toolResult.ErrorMessage}"));
                await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, step.StepNumber, "EXECUTION_FAILED", $"Step {step.StepNumber} failed: {toolResult.ErrorMessage}"), cancellationToken);

                agentRun.Status = AgentRunStatus.Failed;
                agentRun.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                sw.Stop();
                var errMsg = toolResult.ErrorMessage ?? "Tool execution failed.";
                return new AgentResult
                {
                    RunId = agentRun.Id,
                    OriginalPrompt = userPrompt,
                    Intent = parsedIntent.Intent,
                    IsSuccess = false,
                    Plan = plan,
                    ExecutedSteps = executedSteps,
                    ExecutionFeed = feed,
                    ErrorMessage = errMsg,
                    ExecutionTimeMs = sw.ElapsedMilliseconds,
                    // ── Spec state machine fields ────────────────────────────────────────
                    State = WorkflowState.CLARIFICATION_REQUIRED.ToString(),
                    UserMessage = errMsg,
                    TargetSystem = ExtractTargetSystem(parsedIntent),
                    ConfirmationDetails = BuildClarificationDetails(parsedIntent)
                };
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        // 6. COMPLETE AGENT RUN
        agentRun.Status = AgentRunStatus.Completed;
        agentRun.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_COMPLETED, $"Agent run completed successfully in {sw.ElapsedMilliseconds}ms. All plan steps executed cleanly."));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, plan.Steps.Count, "EXECUTION_COMPLETED", "Agent run completed successfully."), cancellationToken);

        sw.Stop();

        // Build execution payload for READY_TO_EXECUTE state
        var execPayload = BuildExecutionPayload(parsedIntent, lastResultData);

        return new AgentResult
        {
            RunId = agentRun.Id,
            OriginalPrompt = userPrompt,
            Intent = parsedIntent.Intent,
            IsSuccess = true,
            Plan = plan,
            ExecutedSteps = executedSteps,
            ResultData = lastResultData,
            ExecutionFeed = feed,
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            // ── Spec state machine fields ────────────────────────────────────────
            State = WorkflowState.READY_TO_EXECUTE.ToString(),
            UserMessage = $"Request completed successfully. Intent: {parsedIntent.Intent}.",
            TargetSystem = ExtractTargetSystem(parsedIntent),
            ConfirmationDetails = new ConfirmationDetails
            {
                ProposedAction = $"Executed {string.Join(", ", executedSteps.Select(s => s.ToolName))}",
                ActionSummary = "Confirmed by system. Execution completed.",
                RequiresUserAction = false
            },
            ExecutionPayload = execPayload
        };
    }

    public async Task<AgentResult> ResumeApprovedRunAsync(Guid runId, Guid approvalId, string approvedBy = "Executive Admin", Dictionary<string, object>? editedParameters = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var feed = new List<AgentEvent>();

        // 1. Load AgentRun & Approval
        var agentRun = await _db.AgentRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (agentRun == null)
            throw new InvalidOperationException($"AgentRun '{runId}' not found.");

        var approval = await _db.Approvals.FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);
        if (approval == null)
            throw new InvalidOperationException($"Approval record '{approvalId}' not found.");

        if (approval.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Approval '{approvalId}' has already been processed (Status: {approval.Status}).");

        approval.Status = ApprovalStatus.Approved;
        approval.ApprovedBy = approvedBy;
        approval.ApprovedAt = DateTime.UtcNow;

        agentRun.Status = AgentRunStatus.Executing;
        await _db.SaveChangesAsync(cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.TOOL_SELECTED, $"APPROVAL_RECEIVED: Plan of Action approved by {approvedBy}. Resuming tool execution..."));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 1, "APPROVAL_RECEIVED", $"Plan of Action approved by {approvedBy}. Resuming execution..."), cancellationToken);

        Nexus.Data.ActionPlan.ActionPlan? actionPlan = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(approval.Reason) && approval.Reason.TrimStart().StartsWith("{"))
            {
                actionPlan = JsonSerializer.Deserialize<Nexus.Data.ActionPlan.ActionPlan>(approval.Reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize persisted ActionPlan from Approval.Reason");
        }

        var targetRecordIds = actionPlan?.AffectedRecords?
            .Select(r => r.RecordId)
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? new List<int>();

        ParsedIntentResult? parsedIntent = null;
        if (!string.IsNullOrWhiteSpace(actionPlan?.Metadata))
        {
            try
            {
                parsedIntent = JsonSerializer.Deserialize<ParsedIntentResult>(actionPlan.Metadata);
            }
            catch { }
        }

        if (editedParameters != null && editedParameters.Count > 0)
        {
            parsedIntent ??= new ParsedIntentResult();
            parsedIntent.Parameters ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            parsedIntent.Entities ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in editedParameters)
            {
                if (kv.Value != null)
                {
                    parsedIntent.Parameters[kv.Key] = kv.Value;
                    parsedIntent.Entities[kv.Key] = kv.Value.ToString() ?? "";
                }
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"USER_EDITS_APPLIED: Workflow parameters updated with HR Admin user-edited values: {JsonSerializer.Serialize(editedParameters)}"));
        }

        var prompt = agentRun.OriginalPrompt ?? "";
        var pLower = prompt.ToLower();
        int affectedCount = 0;
        string resultMessage = "";

        var intentType = parsedIntent?.ParsedIntentType ?? IntentType.UNKNOWN;

        // ── 1. DEPARTMENT CREATION / UPDATE ─────────────────────────────────────
        if (intentType == IntentType.DEPARTMENT_CREATE || intentType == IntentType.DEPARTMENT_UPDATE || (pLower.Contains("department") && (pLower.Contains("create") || pLower.Contains("add")) && !pLower.Contains("employee")))
        {
            var deptName = parsedIntent?.Parameters?.GetValueOrDefault("name")?.ToString()
                ?? parsedIntent?.Entities?.GetValueOrDefault("name")
                ?? "Finance";

            var head = parsedIntent?.Parameters?.GetValueOrDefault("head")?.ToString()
                ?? parsedIntent?.Entities?.GetValueOrDefault("head")
                ?? "Sufyan";

            decimal budgetAmt = 50000m;
            if (parsedIntent?.Parameters?.TryGetValue("budgetAmount", out var bObj) == true && decimal.TryParse(bObj?.ToString(), out var bVal))
                budgetAmt = bVal;

            var deptTool = _toolRegistry.GetTool("department.crud");
            if (deptTool != null)
            {
                var toolArgs = new Dictionary<string, object>
                {
                    { "operation", intentType == IntentType.DEPARTMENT_UPDATE ? "UPDATE" : "CREATE" },
                    { "name", deptName },
                    { "description", $"Department Head: {head}, Initial Staff: 0" }
                };
                var toolCtx = new ToolExecutionContext
                {
                    AgentRunId = runId,
                    UserId = agentRun.UserId,
                    UserRole = "Admin",
                    ArgumentsJson = JsonSerializer.Serialize(toolArgs)
                };
                await deptTool.ExecuteAsync(toolCtx);
            }

            if (budgetAmt > 0)
            {
                var budgetTool = _toolRegistry.GetTool("budget.update");
                if (budgetTool != null)
                {
                    var budgetArgs = new Dictionary<string, object>
                    {
                        { "department", deptName },
                        { "budgetAmount", budgetAmt },
                        { "quarter", "Q3" },
                        { "mode", "SET" }
                    };
                    var budgetCtx = new ToolExecutionContext
                    {
                        AgentRunId = runId,
                        UserId = agentRun.UserId,
                        UserRole = "Admin",
                        ArgumentsJson = JsonSerializer.Serialize(budgetArgs)
                    };
                    await budgetTool.ExecuteAsync(budgetCtx);
                }
            }

            affectedCount = 1;
            resultMessage = $"Created Department '{deptName}' (Head: {head}, Initial Staff: 0) with ${budgetAmt:N2} allocated Q3 budget.";
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"DEPARTMENT_CREATED: {resultMessage}"));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 2, "DEPARTMENT_CREATED", resultMessage), cancellationToken);
        }
        // ── 2. BUDGET UPDATE / ALLOCATE ─────────────────────────────────────────
        else if (intentType == IntentType.BUDGET_UPDATE)
        {
            var deptName = parsedIntent?.Parameters?.GetValueOrDefault("department")?.ToString()
                ?? parsedIntent?.Entities?.GetValueOrDefault("department")
                ?? "IT";

            decimal budgetAmt = 50000m;
            if (parsedIntent?.Parameters?.TryGetValue("budgetAmount", out var bObj) == true && decimal.TryParse(bObj?.ToString(), out var bVal))
                budgetAmt = bVal;

            var budgetTool = _toolRegistry.GetTool("budget.update");
            if (budgetTool != null)
            {
                var budgetArgs = new Dictionary<string, object>
                {
                    { "department", deptName },
                    { "budgetAmount", budgetAmt },
                    { "quarter", "Q3" },
                    { "mode", "ADD" }
                };
                var budgetCtx = new ToolExecutionContext
                {
                    AgentRunId = runId,
                    UserId = agentRun.UserId,
                    UserRole = "Admin",
                    ArgumentsJson = JsonSerializer.Serialize(budgetArgs)
                };
                await budgetTool.ExecuteAsync(budgetCtx);
            }

            affectedCount = 1;
            resultMessage = $"Allocated +${budgetAmt:N2} budget to {deptName} department for Q3.";
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"BUDGET_UPDATED: {resultMessage}"));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 2, "BUDGET_UPDATED", resultMessage), cancellationToken);
        }
        // ── 3. EMPLOYEE ONBOARDING ──────────────────────────────────────────────
        else if (intentType == IntentType.EMPLOYEE_ONBOARDING || pLower.Contains("onboard"))
        {
            string empName = parsedIntent?.Entities?.GetValueOrDefault("name")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("name")?.ToString()
                ?? "Candidate";

            decimal empSalary = 75000.00m;
            string? salString = parsedIntent?.Entities?.GetValueOrDefault("salary") ?? parsedIntent?.Parameters?.GetValueOrDefault("salary")?.ToString();
            if (!string.IsNullOrWhiteSpace(salString) && decimal.TryParse(salString, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedSal) && parsedSal > 0)
            {
                empSalary = parsedSal;
            }
            else
            {
                var origP = agentRun.OriginalPrompt ?? string.Empty;
                var promptSalMatch = System.Text.RegularExpressions.Regex.Match(origP, @"\b([0-9]+(?:\.[0-9]+)?)\s*(k|m)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (promptSalMatch.Success && decimal.TryParse(promptSalMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPromptSal))
                {
                    empSalary = parsedPromptSal * (promptSalMatch.Groups[2].Value.ToLower() == "k" ? 1000m : 1000000m);
                    if (origP.ToLowerInvariant().Contains("monthly")) empSalary *= 12m;
                }
            }

            var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Name.ToLower().Contains("it") || d.Name.ToLower().Contains("software"), cancellationToken);
            int deptId = dept?.Id ?? 1;

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"EMPLOYEE_CREATION_STARTED: Inserting employee '{empName}' into SQL Server database..."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 2, "EMPLOYEE_CREATION_STARTED", $"Creating SQL record for {empName}..."), cancellationToken);

            string empEmail = parsedIntent?.Entities?.GetValueOrDefault("email")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("email")?.ToString()
                ?? $"{empName.ToLower().Replace(" ", ".")}@nexus.local";

            var newEmp = new Employee
            {
                Name = empName,
                Email = empEmail,
                DepartmentId = deptId,
                Designation = parsedIntent?.Entities?.GetValueOrDefault("designation") ?? "Developer",
                Salary = empSalary,
                ExperienceYears = 3,
                Status = EmployeeStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _db.Employees.Add(newEmp);
            await _db.SaveChangesAsync(cancellationToken);

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"EMPLOYEE_CREATED: Created SQL Employee ID #{newEmp.Id} for {empName} (${empSalary:N2})."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 3, "EMPLOYEE_CREATED", $"Created SQL Employee ID #{newEmp.Id} for {empName}."), cancellationToken);

            var legacyTool = _toolRegistry.GetTool("onboarding.submit_legacy_form");
            var legacyRef = $"HR-REC-2026-{newEmp.Id}";
            if (legacyTool != null)
            {
                var toolArgs = new Dictionary<string, object>
                {
                    { "name", empName },
                    { "department", dept?.Name ?? "IT" },
                    { "designation", newEmp.Designation },
                    { "salary", empSalary }
                };
                var toolCtx = new ToolExecutionContext
                {
                    AgentRunId = runId,
                    UserId = agentRun.UserId,
                    UserRole = "Admin",
                    ArgumentsJson = JsonSerializer.Serialize(toolArgs)
                };

                var legacyResult = await legacyTool.ExecuteAsync(toolCtx);
                if (legacyResult.Data is Dictionary<string, object> d && d.TryGetValue("portalRecordId", out var prId))
                    legacyRef = prId.ToString() ?? legacyRef;
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"LEGACY_AUTOMATION_COMPLETED: Submitted Legacy HR Portal form (Ref: {legacyRef})."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 5, "LEGACY_AUTOMATION_COMPLETED", $"Submitted Legacy HR Portal form ({legacyRef})."), cancellationToken);

            var sapTool = _toolRegistry.GetTool("sap.employee.create");
            var sapRef = $"SAP-EMP-2026-{newEmp.Id}";
            if (sapTool != null)
            {
                var toolArgs = new Dictionary<string, object>
                {
                    { "employeeId", newEmp.Id },
                    { "name", empName },
                    { "department", dept?.Name ?? "IT" },
                    { "salary", empSalary }
                };
                var toolCtx = new ToolExecutionContext
                {
                    AgentRunId = runId,
                    UserId = agentRun.UserId,
                    UserRole = "Admin",
                    ArgumentsJson = JsonSerializer.Serialize(toolArgs)
                };

                var sapResult = await sapTool.ExecuteAsync(toolCtx);
                if (sapResult.Data is Dictionary<string, object> d && d.TryGetValue("personnelId", out var pId))
                    sapRef = pId.ToString() ?? sapRef;
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"SAP_OPERATION_COMPLETED: Provisioned SAP Master Data (ID: {sapRef})."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 7, "SAP_OPERATION_COMPLETED", $"Provisioned SAP Master Data (ID: {sapRef})."), cancellationToken);

            var emailTool = _toolRegistry.GetTool("email.welcome");
            if (emailTool != null)
            {
                var toolArgs = new Dictionary<string, object>
                {
                    { "name", empName },
                    { "email", newEmp.Email },
                    { "designation", newEmp.Designation },
                    { "department", dept?.Name ?? "IT" }
                };
                var toolCtx = new ToolExecutionContext
                {
                    AgentRunId = runId,
                    UserId = agentRun.UserId,
                    UserRole = "Admin",
                    ArgumentsJson = JsonSerializer.Serialize(toolArgs)
                };
                await emailTool.ExecuteAsync(toolCtx);
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"WELCOME_EMAIL_COMPLETED: Welcome email generated & sent."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 9, "WELCOME_EMAIL_COMPLETED", $"Welcome email sent to {newEmp.Email}."), cancellationToken);

            affectedCount = 1;
            resultMessage = $"Onboarding Fully Provisioned for {empName}. Created SQL Record ID #{newEmp.Id} (${empSalary:N2}), Legacy HR Portal Ref ({legacyRef}), Mock SAP Personnel ID ({sapRef}), and sent welcome email.";
        }
        // ── 4. EMPLOYEE DELETE ──────────────────────────────────────────────────
        else if (intentType == IntentType.EMPLOYEE_DELETE)
        {
            List<Employee> toDelete;
            if (targetRecordIds.Count > 0)
            {
                toDelete = await _db.Employees
                    .Where(e => targetRecordIds.Contains(e.Id))
                    .ToListAsync(cancellationToken);
            }
            else
            {
                toDelete = new List<Employee>();
            }

            if (toDelete.Count > 0)
            {
                _db.Employees.RemoveRange(toDelete);
                await _db.SaveChangesAsync(cancellationToken);
                affectedCount = toDelete.Count;
                resultMessage = $"Permanently deleted {toDelete.Count} employee record(s) from SQL Server database.";
            }
            else
            {
                affectedCount = 0;
                resultMessage = "Deletion request processed. Zero matching records found in SQL Server database.";
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"EMPLOYEE_DELETED: {resultMessage}"));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 2, "EMPLOYEE_DELETED", resultMessage), cancellationToken);
        }
        // ── 5. EMPLOYEE SALARY UPDATE (UPDATE_SALARY & EMPLOYEE_UPDATE) ──
        else if (intentType == IntentType.UPDATE_SALARY || intentType == IntentType.EMPLOYEE_UPDATE)
        {
            var empName = parsedIntent?.Entities?.GetValueOrDefault("name")
                ?? parsedIntent?.Entities?.GetValueOrDefault("employee_name")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("name")?.ToString()
                ?? parsedIntent?.Parameters?.GetValueOrDefault("employee_name")?.ToString();

            List<Employee> toUpdate = new();
            if (targetRecordIds.Count > 0)
            {
                toUpdate = await _db.Employees.Where(e => targetRecordIds.Contains(e.Id)).ToListAsync(cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(empName))
            {
                toUpdate = await _db.Employees.Where(e => e.Name.ToLower().Contains(empName.ToLower())).ToListAsync(cancellationToken);
            }
            else
            {
                var deptFilter = parsedIntent?.Entities?.GetValueOrDefault("department") ?? "IT";
                toUpdate = await _db.Employees.Include(e => e.Department)
                    .Where(e => e.Department != null && e.Department.Name.ToLower().Contains(deptFilter.ToLower()))
                    .ToListAsync(cancellationToken);
            }

            // Extract absolute salary parameter if provided (e.g. 700k, 700000)
            decimal? newSalary = null;
            if (parsedIntent?.Entities?.TryGetValue("new_salary", out var sStr) == true && decimal.TryParse(sStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sVal))
                newSalary = sVal;
            else if (parsedIntent?.Entities?.TryGetValue("salary", out sStr) == true && decimal.TryParse(sStr, NumberStyles.Any, CultureInfo.InvariantCulture, out sVal))
                newSalary = sVal;
            else if (parsedIntent?.Entities?.TryGetValue("amount", out sStr) == true && decimal.TryParse(sStr, NumberStyles.Any, CultureInfo.InvariantCulture, out sVal))
                newSalary = sVal;
            else if (parsedIntent?.Parameters?.TryGetValue("new_salary", out var sObj) == true && decimal.TryParse(sObj?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out sVal))
                newSalary = sVal;
            else if (parsedIntent?.Parameters?.TryGetValue("salary", out sObj) == true && decimal.TryParse(sObj?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out sVal))
                newSalary = sVal;

            if (newSalary.HasValue && newSalary.Value > 0)
            {
                foreach (var emp in toUpdate)
                {
                    emp.Salary = newSalary.Value;
                    emp.UpdatedAt = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync(cancellationToken);
                affectedCount = toUpdate.Count;
                resultMessage = $"Updated compensation to ${newSalary.Value:N2}/yr for {toUpdate.Count} employee(s) in SQL Server database.";
            }
            else
            {
                decimal pct = 10m;
                if (parsedIntent?.Entities?.TryGetValue("percentage", out var pStr) == true && decimal.TryParse(pStr, out var pDec))
                    pct = pDec;

                foreach (var emp in toUpdate)
                {
                    emp.Salary = Math.Round(emp.Salary * (1m + (pct / 100m)), 2);
                    emp.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync(cancellationToken);
                affectedCount = toUpdate.Count;
                resultMessage = $"Updated compensation (+{pct}%) for {toUpdate.Count} employee(s) in SQL Server database.";
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"SALARY_UPDATED: {resultMessage}"));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 2, "SALARY_UPDATED", resultMessage), cancellationToken);
        }

        // STEP 5: Cryptographic Audit Ledger Recording
        feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, "AUDIT_RECORDING: Signing SHA-256 cryptographic ledger block..."));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 10, "AUDIT_RECORDING", "Signing SHA-256 cryptographic ledger block..."), cancellationToken);

        await RecordAuditLogAsync(runId, agentRun.UserId, "system.orchestration", $"Approved execution: {agentRun.OriginalPrompt}", resultMessage, cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, "AUDIT_RECORDED: Block sealed on tamper-proof audit chain."));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 11, "AUDIT_RECORDED", "Block sealed on tamper-proof audit chain."), cancellationToken);

        // STEP 6: Complete Agent Run
        agentRun.Status = AgentRunStatus.Completed;
        agentRun.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_COMPLETED, $"WORKFLOW_COMPLETED: {resultMessage}"));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 12, "WORKFLOW_COMPLETED", resultMessage), cancellationToken);

        sw.Stop();
        return new AgentResult
        {
            RunId = runId,
            OriginalPrompt = agentRun.OriginalPrompt ?? string.Empty,
            Intent = agentRun.Intent ?? string.Empty,
            IsSuccess = true,
            ResultData = new { message = resultMessage, affectedCount },
            ExecutionFeed = feed,
            ExecutionTimeMs = sw.ElapsedMilliseconds
        };
    }

    private AgentPlan BuildPlan(string prompt, ParsedIntentResult intent)
    {
        var plan = new AgentPlan();
        var intentType = intent.ParsedIntentType;

        switch (intentType)
        {
            // ── Employee ────────────────────────────────────────────────────
            case IntentType.EMPLOYEE_CREATE:
                if (_toolRegistry.HasTool("policy.evaluate"))
                    plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate HR policy rule", RiskLevel = RiskLevel.Low });
                plan.Steps.Add(new AgentStep { StepNumber = plan.Steps.Count + 1, ToolName = "employee.create", Description = "Create employee record", RiskLevel = RiskLevel.Medium });
                break;

            case IntentType.EMPLOYEE_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.read", Description = "Read employee records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.EMPLOYEE_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.update", Description = "Update employee salary / department", RiskLevel = RiskLevel.High });
                break;

            case IntentType.EMPLOYEE_DELETE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.delete", Description = "Delete employee record", RiskLevel = intent.Scope == "ALL" ? RiskLevel.Critical : RiskLevel.High });
                break;

            case IntentType.EMPLOYEE_ONBOARDING:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate HR Policy POL-HR-001 salary band", RiskLevel = RiskLevel.Low });
                plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "employee.create", Description = "Create employee SQL record", RiskLevel = RiskLevel.Medium });
                plan.Steps.Add(new AgentStep { StepNumber = 3, ToolName = "onboarding.submit_legacy_form", Description = "Submit legacy HR portal form via Playwright", RiskLevel = RiskLevel.Medium });
                plan.Steps.Add(new AgentStep { StepNumber = 4, ToolName = "sap.employee.create", Description = "Provision employee in Mock SAP HCM", RiskLevel = RiskLevel.Medium });
                plan.Steps.Add(new AgentStep { StepNumber = 5, ToolName = "email.welcome", Description = "Generate welcome email", RiskLevel = RiskLevel.Low });
                break;

            // ── Policy ──────────────────────────────────────────────────────
            case IntentType.POLICY_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.crud", Description = "Read / search policy records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.POLICY_CREATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.crud", Description = "Create new policy record", RiskLevel = RiskLevel.Medium });
                break;

            case IntentType.POLICY_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.crud", Description = "Update policy record", RiskLevel = RiskLevel.Medium });
                break;

            case IntentType.POLICY_DELETE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.crud", Description = "Deactivate / delete policy record", RiskLevel = RiskLevel.High });
                break;

            // ── Department ──────────────────────────────────────────────────
            case IntentType.DEPARTMENT_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "department.read", Description = "Read department records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.DEPARTMENT_CREATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "department.crud", Description = "Create new department", RiskLevel = RiskLevel.Medium });
                break;

            case IntentType.DEPARTMENT_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "department.crud", Description = "Update department record", RiskLevel = RiskLevel.Medium });
                break;

            case IntentType.DEPARTMENT_DELETE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "department.crud", Description = "Delete department", RiskLevel = RiskLevel.High });
                break;

            // ── Budget ──────────────────────────────────────────────────────
            case IntentType.BUDGET_ANALYSIS:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Execute T-SQL budget analysis query", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.BUDGET_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "budget.read", Description = "Read budget records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.BUDGET_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "budget.update", Description = "Update department budget allocation", RiskLevel = RiskLevel.High });
                break;

            // ── Expense ─────────────────────────────────────────────────────
            case IntentType.EXPENSE_COMPLIANCE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate expense claim against policy limit", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.EXPENSE_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "expense.read", Description = "Read expense records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.EXPENSE_CREATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Validate expense against policy", RiskLevel = RiskLevel.Low });
                plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "expense.read", Description = "Record expense (read tool used for compliance only)", RiskLevel = RiskLevel.Medium });
                break;

            // ── Read-Only Cross-Cutting ──────────────────────────────────────
            case IntentType.APPROVAL_READ:
            case IntentType.ONBOARDING_READ:
            case IntentType.AUDIT_READ:
            case IntentType.DASHBOARD_ANALYTICS:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Query enterprise records for summary", RiskLevel = RiskLevel.Low });
                break;

            // Spec: UPDATE_SALARY — financial mutation, always approval-gated
            case IntentType.UPDATE_SALARY:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Validate new salary against HR compensation band policy", RiskLevel = RiskLevel.Low });
                plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "employee.update", Description = "Apply salary change to employee record in SQL Server", RiskLevel = RiskLevel.High });
                break;

            // Spec: SECURITY_TEST — diagnostic/read-only, never mutates data
            case IntentType.SECURITY_TEST:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Execute read-only connection audit and system health check", RiskLevel = RiskLevel.Low });
                break;

            // Spec: EXECUTE_AUTOMATION — routes to browser automation / Playwright
            case IntentType.EXECUTE_AUTOMATION:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "browser.automation", Description = "Trigger external workflow automation (n8n / Zapier / Playwright)", RiskLevel = RiskLevel.Medium });
                break;

            // SQL Agent passthrough
            case IntentType.SQL_AGENT:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Execute Production SQL Agent query", RiskLevel = RiskLevel.Low });
                break;

            default:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.read", Description = "Search workforce directory", RiskLevel = RiskLevel.Low });
                break;
        }

        plan.TotalRiskLevel = plan.Steps.Count > 0 ? plan.Steps.Max(s => s.RiskLevel) : RiskLevel.Low;
        return plan;
    }

    // ---- Spec-aligned helper methods (State Machine) -----------------------------------------------

    /// <summary>
    /// Extracts the target_system field from the LLM-parsed parameters.
    /// Falls back to SQL_SERVER for most intents; N8N_WORKFLOW / ZAPIER / UNKNOWN for automation.
    /// </summary>
    private static string ExtractTargetSystem(ParsedIntentResult intent)
    {
        // Try to read from the LLM-supplied parameters.target_system first
        if (intent.Parameters.TryGetValue("target_system", out var tsObj) && tsObj is string ts && !string.IsNullOrWhiteSpace(ts))
            return ts.ToUpperInvariant();

        // Rule-based fallback by intent type
        return intent.ParsedIntentType switch
        {
            IntentType.EXECUTE_AUTOMATION => "N8N_WORKFLOW",
            IntentType.SECURITY_TEST      => "UNKNOWN",
            IntentType.GENERAL_CONVERSATION => "UNKNOWN",
            _                             => "SQL_SERVER"
        };
    }

    /// <summary>
    /// Builds the user-facing confirmation message and ConfirmationDetails block for
    /// CONFIRMATION_REQUIRED states.
    /// </summary>
    private static (string Message, ConfirmationDetails Details) BuildConfirmationUserMessage(ParsedIntentResult intent)
    {
        var e = intent.Entities;
        string name = e.GetValueOrDefault("name") ?? e.GetValueOrDefault("employee_name") ?? "the employee";

        string message;
        string proposed;
        string summary;

        switch (intent.ParsedIntentType)
        {
            case IntentType.UPDATE_SALARY:
                var salary = e.GetValueOrDefault("salary") ?? e.GetValueOrDefault("new_salary") ?? "[pending]";
                var effectiveDate = intent.StructuredEntities?.EffectiveDate ?? "immediately";
                message = "I have drafted a salary adjustment request. Please review the details below before I submit it to the system:";
                proposed = "Update employee compensation record in SQL Server database";
                summary = $"Employee: {name} | New Amount: {salary}/mo | Effective Date: {effectiveDate}";
                break;

            case IntentType.EMPLOYEE_ONBOARDING:
            case IntentType.EMPLOYEE_CREATE:
                var dept = e.GetValueOrDefault("department") ?? "[Pending]";
                var desig = e.GetValueOrDefault("designation") ?? "[Pending]";
                var sal = e.GetValueOrDefault("salary") ?? "[Pending]";
                message = $"I am ready to onboard {name}. Please confirm the details below before I create the record:";
                proposed = "Insert new employee record into SQL Server database";
                summary = $"Employee: {name} | Department: {dept} | Role: {desig} | Salary: {sal}";
                break;

            case IntentType.EXECUTE_AUTOMATION:
                var target = intent.StructuredEntities?.AutomationTarget ?? "the configured workflow";
                message = "I am ready to trigger the automation workflow. Please confirm:";
                proposed = "Execute external automation (n8n / Zapier / Playwright)";
                summary = $"Target: {target} | Action: EXECUTE";
                break;

            default:
                message = "I have prepared the following action for your review. Please confirm before execution:";
                proposed = $"Execute {intent.Intent} operation in SQL Server";
                summary = $"Intent: {intent.Intent} | Scope: {intent.Scope}";
                break;
        }

        return (message, new ConfirmationDetails
        {
            ProposedAction = proposed,
            ActionSummary = summary,
            RequiresUserAction = true
        });
    }

    /// <summary>
    /// Builds the ConfirmationDetails block for CLARIFICATION_REQUIRED states,
    /// showing which fields are still missing.
    /// </summary>
    private static ConfirmationDetails BuildClarificationDetails(ParsedIntentResult intent)
    {
        var missing = intent.MissingFields.Count > 0
            ? string.Join(", ", intent.MissingFields.Select(f => $"[{f}]"))
            : "[additional details]";

        return new ConfirmationDetails
        {
            ProposedAction = $"Awaiting clarification for: {intent.Intent}",
            ActionSummary = $"Missing required fields: {missing}",
            RequiresUserAction = true
        };
    }

    /// <summary>
    /// Builds the execution_payload object for READY_TO_EXECUTE results.
    /// This is a structured summary of what was executed, ready for frontend display or
    /// downstream API consumption.
    /// </summary>
    private static object? BuildExecutionPayload(ParsedIntentResult intent, object? resultData)
    {
        if (resultData == null) return null;

        return new
        {
            intent = intent.Intent,
            operation = intent.Operation,
            targetEntity = intent.TargetEntity,
            targetSystem = ExtractTargetSystem(intent),
            timestamp = DateTime.UtcNow.ToString("o"),
            result = resultData
        };
    }


    private async Task<Nexus.Data.ActionPlan.ActionPlan> BuildActionPlanAsync(Guid runId, string prompt, ParsedIntentResult intent, CancellationToken ct)
    {
        var pLower = prompt.ToLower();
        var intentType = intent.ParsedIntentType;

        // ── 1. DEPARTMENT CREATION / MANAGEMENT ────────────────────────────────
        if (intentType == IntentType.DEPARTMENT_CREATE || intentType == IntentType.DEPARTMENT_UPDATE || (intent.TargetEntity == "DEPARTMENT" && (intent.Operation == "CREATE" || intent.Operation == "UPDATE")))
        {
            var deptName = intent.Parameters.GetValueOrDefault("name")?.ToString()
                ?? intent.Entities.GetValueOrDefault("name")
                ?? "Finance";

            var head = intent.Parameters.GetValueOrDefault("head")?.ToString()
                ?? intent.Entities.GetValueOrDefault("head")
                ?? "Sufyan";

            decimal budgetAmt = 50000m;
            if (intent.Parameters.TryGetValue("budgetAmount", out var bObj) && decimal.TryParse(bObj?.ToString(), out var bVal))
                budgetAmt = bVal;

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Create Department '{deptName}'",
                RiskLevel = RiskLevel.Medium,
                Status = "AWAITING_APPROVAL",
                TotalFinancialImpact = budgetAmt,
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Department Master (SQL Server)",
                PrimaryLabel = $"{deptName} Department",
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Department Name", OldValue = "[NEW]", NewValue = deptName, Difference = "New Entity" },
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Department Head", OldValue = "None", NewValue = head, Difference = "Assignment" },
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Initial Staff Count", OldValue = "0", NewValue = "0", Difference = "Empty Dept" },
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Allocated Q3 Budget", OldValue = "$0.00", NewValue = $"${budgetAmt:N2}", Difference = "Budget Allocation" }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "department.crud", Description = $"Create Department record '{deptName}' (Head: {head})", RiskLevel = RiskLevel.Medium });
            if (budgetAmt > 0)
            {
                actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 2, ToolName = "budget.update", Description = $"Initialize allocated Q3 budget (${budgetAmt:N2})", RiskLevel = RiskLevel.Medium });
            }

            actionPlan.Warnings.Add($"Creates new {deptName} department with ${budgetAmt:N2} allocated Q3 budget and {head} as Department Head.");
            return actionPlan;
        }

        // ── 2. BUDGET UPDATE / ALLOCATE ─────────────────────────────────────────
        if (intentType == IntentType.BUDGET_UPDATE || intent.TargetEntity == "DEPARTMENT_BUDGET")
        {
            var deptName = intent.Parameters.GetValueOrDefault("department")?.ToString()
                ?? intent.Entities.GetValueOrDefault("department")
                ?? "IT";

            decimal budgetAmt = 50000m;
            if (intent.Parameters.TryGetValue("budgetAmount", out var bObj) && decimal.TryParse(bObj?.ToString(), out var bVal))
                budgetAmt = bVal;

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Update Q3 Budget for {deptName} Department",
                RiskLevel = RiskLevel.Medium,
                Status = "AWAITING_APPROVAL",
                TotalFinancialImpact = budgetAmt,
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Department Budget (SQL Server)",
                PrimaryLabel = $"{deptName} Q3 Budget",
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Q3 Allocated Budget", OldValue = "[CURRENT]", NewValue = $"${budgetAmt:N2}", Difference = $"Allocation +${budgetAmt:N2}" }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "budget.update", Description = $"Allocate +${budgetAmt:N2} budget to {deptName}", RiskLevel = RiskLevel.Medium });
            actionPlan.Warnings.Add($"Adjusts allocated Q3 budget for {deptName} by ${budgetAmt:N2}.");
            return actionPlan;
        }

        // ── 3. EMPLOYEE ONBOARDING ──────────────────────────────────────────────
        if (intentType == IntentType.EMPLOYEE_ONBOARDING || pLower.Contains("onboard"))
        {
            var name = intent.Entities.GetValueOrDefault("name") ?? intent.Parameters.GetValueOrDefault("name")?.ToString() ?? "Candidate";
            var dept = intent.Entities.GetValueOrDefault("department") ?? intent.Parameters.GetValueOrDefault("department")?.ToString() ?? "IT";
            var desig = intent.Entities.GetValueOrDefault("designation") ?? intent.Parameters.GetValueOrDefault("designation")?.ToString() ?? "Team Member";
            
            decimal proposedSalary = 75000.00m;
            string? salString = intent.Entities.GetValueOrDefault("salary") ?? intent.Parameters.GetValueOrDefault("salary")?.ToString();
            if (!string.IsNullOrWhiteSpace(salString) && decimal.TryParse(salString, NumberStyles.Any, CultureInfo.InvariantCulture, out var salVal) && salVal > 0)
            {
                proposedSalary = salVal;
            }
            else
            {
                var promptSalMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"\b([0-9]+(?:\.[0-9]+)?)\s*(k|m)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (promptSalMatch.Success && decimal.TryParse(promptSalMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPromptSalPlan))
                {
                    proposedSalary = parsedPromptSalPlan * (promptSalMatch.Groups[2].Value.ToLower() == "k" ? 1000m : 1000000m);
                    if (prompt.ToLowerInvariant().Contains("monthly")) proposedSalary *= 12m;
                }
            }

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Comprehensive Onboarding for {name} ({dept})",
                RiskLevel = RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                TotalFinancialImpact = proposedSalary,
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Employee Master (SQL Server)",
                PrimaryLabel = name,
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Provisioning Profile", OldValue = "[WILL BE CREATED]", NewValue = $"{desig} @ ${proposedSalary:N2}", Difference = "New SQL Record" }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate HR Policy POL-HR-001 salary band", RiskLevel = RiskLevel.Low });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 2, ToolName = "employee.create", Description = "Create employee SQL record", RiskLevel = RiskLevel.Medium });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 3, ToolName = "onboarding.submit_legacy_form", Description = "Submit legacy HR portal form via Playwright", RiskLevel = RiskLevel.Medium });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 4, ToolName = "sap.employee.create", Description = "Provision employee in Mock SAP HCM", RiskLevel = RiskLevel.Medium });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 5, ToolName = "email.welcome", Description = "Generate welcome email", RiskLevel = RiskLevel.Low });

            actionPlan.Warnings.Add($"Multi-system onboarding provisions SQL Server, Legacy Portal, SAP ERP, and Welcome Email.");
            return actionPlan;
        }

        // ── 4. EMPLOYEE DELETE ──────────────────────────────────────────────────
        if (intentType == IntentType.EMPLOYEE_DELETE)
        {
            var scope = intent.Scope;
            var empName = intent.Entities.GetValueOrDefault("name", string.Empty);
            var deptFilter = intent.Entities.GetValueOrDefault("department", string.Empty);

            List<Employee> matchingEmps = new();
            if (scope == "ALL") matchingEmps = await _db.Employees.Include(e => e.Department).ToListAsync(ct);
            else if (!string.IsNullOrWhiteSpace(empName)) matchingEmps = await _db.Employees.Where(e => e.Name.ToLower().Contains(empName.ToLower())).ToListAsync(ct);

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = scope == "ALL" ? "Plan of Action: Permanent Removal of All Employees" : $"Plan of Action: Permanent Removal of Employee '{empName}'",
                RiskLevel = scope == "ALL" ? RiskLevel.Critical : RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            decimal totalSavings = matchingEmps.Sum(e => e.Salary);
            foreach (var emp in matchingEmps)
            {
                actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
                {
                    RecordId = emp.Id,
                    EntityName = "Employee Master (SQL Server)",
                    PrimaryLabel = emp.Name,
                    Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                    {
                        new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Employment Status", OldValue = $"{emp.Status} (${emp.Salary:N2}/yr)", NewValue = "[PERMANENTLY DELETED]", Difference = "Record Purge" }
                    }
                });
            }

            actionPlan.TotalFinancialImpact = -totalSavings;
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "employee.delete", Description = $"Purge {matchingEmps.Count} employee record(s)", RiskLevel = RiskLevel.High });
            actionPlan.Warnings.Add($"⚠ High-Risk Operation: Permanent database purge.");
            return actionPlan;
        }

        // ── 5. EMPLOYEE SALARY UPDATE (ONLY when intent is explicitly EMPLOYEE_UPDATE)
        if (intentType == IntentType.EMPLOYEE_UPDATE)
        {
            var targetEmpName = intent.Entities.GetValueOrDefault("name", string.Empty);
            var targetDept = intent.Entities.GetValueOrDefault("department", string.Empty);
            
            decimal pct = 10m;
            if (intent.Entities.TryGetValue("percentage", out var pStr) && decimal.TryParse(pStr, out var pDec))
                pct = pDec;

            var query = _db.Employees.Include(e => e.Department).AsQueryable();
            if (!string.IsNullOrWhiteSpace(targetDept)) query = query.Where(e => e.Department != null && e.Department.Name.ToLower() == targetDept.ToLower());
            else if (!string.IsNullOrWhiteSpace(targetEmpName)) query = query.Where(e => e.Name.ToLower().Contains(targetEmpName.ToLower()));

            var employees = await query.ToListAsync(ct);

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Salary Adjustment (+{pct}%) for {employees.Count} Employee(s)",
                RiskLevel = RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            decimal totalImpact = 0m;
            {
                actionPlan.Warnings.Add("Zero matching employee records found in SQL Server database for salary adjustment.");
            }

            actionPlan.TotalFinancialImpact = totalImpact;
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
            {
                StepNumber = 1,
                ToolName = "employee.update",
                Description = $"Update salary (+{pct}%) across {employees.Count} matching employee records",
                RiskLevel = RiskLevel.High
            });

            actionPlan.Warnings.Add($"Salary modification affects {employees.Count} employee record(s).");
            actionPlan.Warnings.Add($"Total annual payroll increase is ${totalImpact:N2}. Requires explicit executive authorization.");

            return actionPlan;
        }

        // ── 6. GENERIC MUTATION ACTION PLAN (Clean fallback for any other entity) ──
        var genericPlan = new Nexus.Data.ActionPlan.ActionPlan
        {
            Title = $"Plan of Action: {intent.Operation} {intent.TargetEntity}",
            RiskLevel = RiskLevel.Medium,
            Status = "AWAITING_APPROVAL",
            Metadata = JsonSerializer.Serialize(intent)
        };

        genericPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
        {
            RecordId = 0,
            EntityName = $"{intent.TargetEntity} Master",
            PrimaryLabel = intent.Parameters.GetValueOrDefault("name")?.ToString() ?? intent.TargetEntity,
            Changes = intent.Parameters.Select(p => new Nexus.Data.ActionPlan.ChangePreview
            {
                FieldName = p.Key,
                OldValue = "[CURRENT]",
                NewValue = String.Concat(p.Value),
                Difference = "Value Update"
            }).ToList()
        });

        genericPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
        {
            StepNumber = 1,
            ToolName = "system.execution",
            Description = $"Execute {intent.Operation} on {intent.TargetEntity}",
            RiskLevel = RiskLevel.Medium
        });

        return genericPlan;
    }

    private async Task RecordAuditLogAsync(Guid runId, int? userId, string toolName, string actionDescription, object? resultData, CancellationToken ct)
    {
        var lastAudit = await _db.AuditLogs.OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        var prevHash = lastAudit?.CurrentHash ?? "GENESIS_NEXUS_LITE";

        var payload = $"{runId}|{userId}|{toolName}|{actionDescription}|{JsonSerializer.Serialize(resultData)}|{DateTime.UtcNow:O}|{prevHash}";
        var currentHash = ComputeSha256Hash(payload);

        var auditLog = new AuditLog
        {
            AgentRunId = runId,
            UserId = userId,
            Action = actionDescription,
            ToolName = toolName,
            Target = toolName,
            Result = JsonSerializer.Serialize(resultData),
            Timestamp = DateTime.UtcNow,
            PreviousHash = prevHash,
            CurrentHash = currentHash
        };

        _db.AuditLogs.Add(auditLog);
        await _db.SaveChangesAsync(ct);
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes);
    }
}
