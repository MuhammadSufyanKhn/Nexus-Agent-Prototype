using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Agent.Orchestration;
using Nexus.Data;
using Nexus.Data.Entities;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AutomationController : ControllerBase
{
    private readonly NexusDbContext _db;
    private readonly IAgentOrchestrator _orchestrator;

    public AutomationController(NexusDbContext db, IAgentOrchestrator orchestrator)
    {
        _db = db;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// GET /api/automation/workflows - Runnable Enterprise Workflows Catalog
    /// </summary>
    [HttpGet("workflows")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetWorkflows()
    {
        var workflows = new[]
        {
            new
            {
                id = "wf-onboarding",
                title = "End-to-End Workforce Onboarding Pipeline",
                subsystem = "Onboarding & HR Provisioning",
                description = "Automates employee profile creation, policy compensation check (POL-HR-001), SAP HCM sync, Onboarding Package HTML doc generation, welcome email, and IT ticket creation.",
                defaultPrompt = "Onboard new employee Sarah Jenkins as Senior Software Engineer in Engineering with $120,000 salary.",
                icon = "UserPlus",
                steps = new[] { "Employee Data Extraction", "Compensation Policy Check", "SQL Master DB Sync", "SAP ERP HCM Provisioning", "Document Generator", "Welcome Email", "IT Hardware Ticket" }
            },
            new
            {
                id = "wf-expense-audit",
                title = "Corporate Expense Compliance & Flagging Sweep",
                subsystem = "Finance & Expense Compliance",
                description = "Scans all employee expense claims against corporate policy POL-FIN-002. Flags meal/travel limit overflows, computes variance, updates DB status, and routes to approvals.",
                defaultPrompt = "Run corporate expense compliance sweep against POL-FIN-002 and flag non-compliant claims.",
                icon = "Receipt",
                steps = new[] { "Scan Active Claims", "Policy Rules Evaluation", "Overflow Calculation", "Status Update (NonCompliant/Compliant)", "Manager Approval Routing", "Audit Hash Generation" }
            },
            new
            {
                id = "wf-it-provisioning",
                title = "IT Hardware & Credential Access Pipeline",
                subsystem = "IT Infrastructure & Security",
                description = "Automates hardware ticket creation, priority triaging, software license assignments, and security credential notifications for engineering and operations.",
                defaultPrompt = "Provision IT hardware ticket for dual 27-inch 4K monitors and AWS VPN access for Sarah in Engineering.",
                icon = "Ticket",
                steps = new[] { "Parse Request Details", "AI Categorization & Priority", "Hardware Inventory Allocation", "Ticket State Transition", "Notification Dispatch" }
            },
            new
            {
                id = "wf-budget-reallocate",
                title = "Department Budget Audit & Reallocation Sweep",
                subsystem = "Financial Planning & Budgets",
                description = "Audits department budget utilization, detects frozen or over-budget departments, and reallocates surplus capital between department pools.",
                defaultPrompt = "Audit department budgets and reallocate $50,000 from Marketing to Engineering.",
                icon = "Building2",
                steps = new[] { "Budget Database Scan", "Over-budget Risk Assessment", "Freeze Status Verification", "Reallocation Mutation", "Cryptographic Log Block" }
            }
        };

        return Ok(workflows);
    }

    /// <summary>
    /// POST /api/automation/execute - Execute Enterprise Automated Pipeline
    /// </summary>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(AgentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentResult>> ExecuteWorkflow([FromBody] ExecuteWorkflowRequest request)
    {
        string promptToRun = request.Prompt;
        if (string.IsNullOrWhiteSpace(promptToRun))
        {
            promptToRun = request.WorkflowId switch
            {
                "wf-onboarding" => "Onboard new employee Sarah Jenkins as Senior Software Engineer in Engineering with $120,000 salary.",
                "wf-expense-audit" => "Run corporate expense compliance sweep against POL-FIN-002 and flag non-compliant claims.",
                "wf-it-provisioning" => "Provision IT hardware ticket for dual 27-inch 4K monitors and AWS VPN access for Sarah in Engineering.",
                "wf-budget-reallocate" => "Audit department budgets and reallocate $50,000 from Marketing to Engineering.",
                _ => "Run automated enterprise workflow sweep across connected modules."
            };
        }

        var result = await _orchestrator.ExecuteAsync(promptToRun, null, request.UserRole ?? "Admin");
        return Ok(result);
    }

    /// <summary>
    /// GET /api/automation/subsystems - Subsystems Readiness & Health Status
    /// </summary>
    [HttpGet("subsystems")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubsystemStatuses()
    {
        var empCount = await _db.Employees.CountAsync();
        var expCount = await _db.Expenses.CountAsync();
        var tckCount = await _db.Tickets.CountAsync();

        var subsystems = new[]
        {
            new
            {
                name = "EF Core SQL Server Master Database",
                type = "Primary Persistence",
                status = "ONLINE & HEALTHY",
                target = "Master DB / LocalDB",
                metrics = $"{empCount} Employees, {expCount} Expenses, {tckCount} IT Tickets stored.",
                isHealthy = true
            },
            new
            {
                name = "Mock SAP ERP HCM Provisioning Connector",
                type = "Enterprise Resource Connector",
                status = "ONLINE & READY",
                target = "SAP Personnel Master API",
                metrics = "Personnel Range SAP-EMP-2026-8900+",
                isHealthy = true
            },
            new
            {
                name = "Python Email Automation Engine",
                type = "Notification Subsystem",
                status = "ONLINE & READY",
                target = "automation/email_services/",
                metrics = "Dispatches HTML Welcome Credentials",
                isHealthy = true
            },
            new
            {
                name = "Nexus Document Generator Engine",
                type = "Document Processing",
                status = "ONLINE & READY",
                target = "GeneratedDocuments Service",
                metrics = "HTML/PDF Onboarding & Orientation Packages",
                isHealthy = true
            },
            new
            {
                name = "Gemini API Function Calling Intelligence Layer",
                type = "AI Reasoning Engine",
                status = "ONLINE & ACTIVE",
                target = "gemini-2.0-flash / generativelanguage.googleapis.com",
                metrics = "Native Tool Calling & Parameter Extraction",
                isHealthy = true
            }
        };

        return Ok(subsystems);
    }

    /// <summary>
    /// GET /api/automation/history - Automation Execution Run History
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory()
    {
        var runs = await _db.AgentRuns
            .Include(r => r.Actions)
            .Include(r => r.AuditLogs)
            .OrderByDescending(r => r.StartedAt)
            .Take(10)
            .AsNoTracking()
            .ToListAsync();

        var result = runs.Select(r => new
        {
            runId = r.Id,
            originalPrompt = r.OriginalPrompt,
            intent = r.Intent,
            status = r.Status.ToString(),
            startedAt = r.StartedAt,
            completedAt = r.CompletedAt,
            actionCount = r.Actions.Count,
            auditLogs = r.AuditLogs.Select(a => new
            {
                action = a.Action,
                target = a.Target,
                result = a.Result,
                currentHash = a.CurrentHash,
                timestamp = a.Timestamp
            })
        });

        return Ok(result);
    }
}

public class ExecuteWorkflowRequest
{
    public string WorkflowId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? UserRole { get; set; } = "Admin";
}
