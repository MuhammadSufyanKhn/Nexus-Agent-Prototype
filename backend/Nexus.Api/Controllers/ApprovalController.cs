using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Agent.Intent;
using Nexus.Agent.Orchestration;
using Nexus.Data;
using Nexus.Data.ActionPlan;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Data.LLM;
using Nexus.Data.Services;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Nexus.Tools.Sap;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApprovalController : ControllerBase
{
    private readonly NexusDbContext _db;
    private readonly IAgentOrchestrator? _orchestrator;

    public ApprovalController(NexusDbContext db, IAgentOrchestrator? orchestrator = null)
    {
        _db = db;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// GET /api/approval/pending - List all pending human-in-the-loop approvals
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<Approval>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Approval>>> GetPendingApprovals(CancellationToken cancellationToken = default)
    {
        var pending = await _db.Approvals
            .Include(a => a.AgentRun)
            .Where(a => a.Status == ApprovalStatus.Pending)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(pending);
    }

    /// <summary>
    /// POST /api/approval/decide - Confirm or reject a pending Plan of Action
    /// </summary>
    [HttpPost("decide")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DecideApproval([FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || request.ApprovalId == Guid.Empty)
        {
            return BadRequest(new { message = "Invalid approval request." });
        }

        var approval = await _db.Approvals
            .Include(a => a.AgentRun)
            .FirstOrDefaultAsync(a => a.Id == request.ApprovalId, cancellationToken);

        if (approval == null)
        {
            return NotFound(new { message = $"Approval record ID '{request.ApprovalId}' not found." });
        }

        if (approval.Status != ApprovalStatus.Pending)
        {
            return BadRequest(new { message = $"Approval record '{request.ApprovalId}' has already been processed (Status: {approval.Status})." });
        }

        var approvedBy = string.IsNullOrWhiteSpace(request.ApprovedBy) ? "Executive Admin" : request.ApprovedBy;

        if (request.Approved)
        {
            try
            {
                var orchestrator = _orchestrator
                    ?? HttpContext?.RequestServices?.GetService<IAgentOrchestrator>()
                    ?? CreateDefaultOrchestrator(_db);

                var result = await orchestrator.ResumeApprovedRunAsync(approval.AgentRunId, approval.Id, approvedBy, request.EditedParameters, cancellationToken);
                return Ok(new { message = $"Plan of Action approved and executed by {approvedBy}.", result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Failed to execute approved Plan of Action: {ex.Message}" });
            }
        }
        else
        {
            approval.Status = ApprovalStatus.Rejected;
            approval.ApprovedBy = approvedBy;
            approval.ApprovedAt = DateTime.UtcNow;
            approval.Reason = request.Reason ?? "Rejected by executive user.";

            if (approval.AgentRun != null)
            {
                approval.AgentRun.Status = AgentRunStatus.Rejected;
                approval.AgentRun.CompletedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = $"Plan of Action rejected by {approvedBy}. Execution cancelled." });
        }
    }

    private static IAgentOrchestrator CreateDefaultOrchestrator(NexusDbContext db)
    {
        var empService = new EmployeeService(db);

        var docService = new PdfDocumentService(db);
        var pythonService = new PythonAutomationService(NullLogger<PythonAutomationService>.Instance);
        var sapConnector = new MockSapConnector(NullLogger<MockSapConnector>.Instance);
        var registry = new ToolRegistry();
        registry.RegisterTool(new WelcomeEmailTool(pythonService, NullLogger<WelcomeEmailTool>.Instance));
        registry.RegisterTool(new CreateTicketTool(pythonService, NullLogger<CreateTicketTool>.Instance));
        registry.RegisterTool(new BrowserAutomationTool(pythonService, NullLogger<BrowserAutomationTool>.Instance));
        registry.RegisterTool(new MockSapTool(sapConnector, NullLogger<MockSapTool>.Instance));
        registry.RegisterTool(new EmployeeCreateTool(empService));
        registry.RegisterTool(new EmployeeReadTool(empService));
        registry.RegisterTool(new EmployeeUpdateTool(empService));
        registry.RegisterTool(new EmployeeDeleteTool(empService));
        registry.RegisterTool(new DepartmentCrudTool(db, NullLogger<DepartmentCrudTool>.Instance));
        registry.RegisterTool(new BudgetUpdateTool(db, NullLogger<BudgetUpdateTool>.Instance));
        registry.RegisterTool(new PolicyCrudTool(db, NullLogger<PolicyCrudTool>.Instance));
        registry.RegisterTool(new EmployeeTransferTool(db, NullLogger<EmployeeTransferTool>.Instance));
        registry.RegisterTool(new EmployeeOffboardTool(db, NullLogger<EmployeeOffboardTool>.Instance));
        registry.RegisterTool(new LeaveTool(db, NullLogger<LeaveTool>.Instance));
        registry.RegisterTool(new SlackNotifyTool(new HttpClient(), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), NullLogger<SlackNotifyTool>.Instance));
        registry.RegisterTool(new BudgetReallocateTool(db, NullLogger<BudgetReallocateTool>.Instance));
        registry.RegisterTool(new BudgetFreezeTool(db, NullLogger<BudgetFreezeTool>.Instance));
        registry.RegisterTool(new PayrollActionTool(db, NullLogger<PayrollActionTool>.Instance));
        registry.RegisterTool(new BulkEmployeeUpdateTool(db, NullLogger<BulkEmployeeUpdateTool>.Instance));
        registry.RegisterTool(new OnboardingPrepareTool(docService, db));
        registry.RegisterTool(new OffboardingInitiateTool(docService, db));
        registry.RegisterTool(new OrientationScheduleTool(docService, db));

        var llm = new LocalLLMService(new HttpClient(), Options.Create(new LLMOptions()), NullLogger<LocalLLMService>.Instance);
        var intentParser = new IntentParser(llm, NullLogger<IntentParser>.Instance);
        return new AgentOrchestrator(intentParser, registry, db, NullLogger<AgentOrchestrator>.Instance);
    }
}
