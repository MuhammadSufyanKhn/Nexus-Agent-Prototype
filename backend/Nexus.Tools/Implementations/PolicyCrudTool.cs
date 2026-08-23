using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Handles CRUD operations for the Policies table via natural-language intent.
/// Supports: READ (search/list), CREATE, UPDATE, DELETE.
/// </summary>
public class PolicyCrudTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<PolicyCrudTool> _logger;

    public PolicyCrudTool(NexusDbContext db, ILogger<PolicyCrudTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "policy.crud",
        Description = "Create, read, update or delete HR policies. Supports natural-language policy lookup by title, code, or category.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""operation"": { ""type"": ""string"", ""enum"": [""READ"",""CREATE"",""UPDATE"",""DELETE""] },
            ""policyTitle"": { ""type"": ""string"" },
            ""policyCode"":  { ""type"": ""string"" },
            ""category"":    { ""type"": ""string"" },
            ""summary"":     { ""type"": ""string"" },
            ""documentPath"":{ ""type"": ""string"" },
            ""isActive"":    { ""type"": ""boolean"" }
          },
          ""required"": [""operation""]
        }",
        RequiredPermission = "policy.read",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
        => Task.FromResult(ValidationResult.Success());

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var operation = context.GetArgument<string>("operation")?.ToUpperInvariant() ?? "READ";
            var policyTitle = context.GetArgument<string>("policyTitle")
                ?? context.GetArgument<string>("prompt")
                ?? string.Empty;
            var policyCode = context.GetArgument<string>("policyCode") ?? string.Empty;

            switch (operation)
            {
                case "READ":
                    return await ReadPoliciesAsync(context, policyTitle, policyCode, sw);
                case "CREATE":
                    return await CreatePolicyAsync(context, policyTitle, sw);
                case "UPDATE":
                    return await UpdatePolicyAsync(context, policyTitle, policyCode, sw);
                case "DELETE":
                    return await DeletePolicyAsync(context, policyTitle, policyCode, sw);
                default:
                    return await ReadPoliciesAsync(context, policyTitle, policyCode, sw);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PolicyCrudTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }

    private async Task<ToolExecutionResult> ReadPoliciesAsync(ToolExecutionContext ctx, string titleSearch, string codeSearch, Stopwatch sw)
    {
        var query = _db.Policies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(codeSearch))
            query = query.Where(p => p.Code.ToLower().Contains(codeSearch.ToLower()));
        else if (!string.IsNullOrWhiteSpace(titleSearch) && titleSearch.Length > 2)
            query = query.Where(p =>
                p.Title.ToLower().Contains(titleSearch.ToLower()) ||
                p.Category.ToLower().Contains(titleSearch.ToLower()) ||
                p.ContentSummary.ToLower().Contains(titleSearch.ToLower()));

        var policies = await query.OrderByDescending(p => p.UpdatedAt).Take(20).ToListAsync();

        var resultData = new
        {
            count = policies.Count,
            policies = policies.Select(p => new
            {
                p.Id,
                p.Code,
                p.Title,
                p.Category,
                p.ContentSummary,
                p.DocumentPath,
                p.IsActive,
                UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-dd")
            }).ToList()
        };

        sw.Stop();
        return ToolExecutionResult.Success(resultData, Definition.RiskLevel, sw.ElapsedMilliseconds);
    }

    private async Task<ToolExecutionResult> CreatePolicyAsync(ToolExecutionContext ctx, string policyTitle, Stopwatch sw)
    {
        var title = !string.IsNullOrWhiteSpace(policyTitle) ? policyTitle
            : ctx.GetArgument<string>("prompt") ?? "New Policy";
        var category = ctx.GetArgument<string>("category") ?? "General";
        var summary = ctx.GetArgument<string>("summary") ?? string.Empty;
        var docPath = ctx.GetArgument<string>("documentPath") ?? string.Empty;

        var count = await _db.Policies.CountAsync();
        var code = $"POL-{category.ToUpper()[..Math.Min(3, category.Length)]}-{count + 1:D3}";

        var policy = new Policy
        {
            Code = code,
            Title = title,
            Category = category,
            ContentSummary = summary,
            DocumentPath = docPath,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Policies.Add(policy);
        await _db.SaveChangesAsync();
        sw.Stop();

        return ToolExecutionResult.Success(new
        {
            message = $"Policy '{title}' created successfully.",
            policy.Id, policy.Code, policy.Title, policy.Category
        }, RiskLevel.Medium, sw.ElapsedMilliseconds);
    }

    private async Task<ToolExecutionResult> UpdatePolicyAsync(ToolExecutionContext ctx, string titleSearch, string codeSearch, Stopwatch sw)
    {
        Policy? policy = null;
        if (!string.IsNullOrWhiteSpace(codeSearch))
            policy = await _db.Policies.FirstOrDefaultAsync(p => p.Code.ToLower() == codeSearch.ToLower());
        if (policy == null && !string.IsNullOrWhiteSpace(titleSearch))
            policy = await _db.Policies.FirstOrDefaultAsync(p => p.Title.ToLower().Contains(titleSearch.ToLower()));

        if (policy == null)
            return ToolExecutionResult.Failure($"Policy '{titleSearch ?? codeSearch}' not found.", RiskLevel.Low);

        var newTitle = ctx.GetArgument<string>("newTitle");
        var newSummary = ctx.GetArgument<string>("summary");
        var newDocPath = ctx.GetArgument<string>("documentPath");

        if (!string.IsNullOrWhiteSpace(newTitle)) policy.Title = newTitle;
        if (!string.IsNullOrWhiteSpace(newSummary)) policy.ContentSummary = newSummary;
        if (!string.IsNullOrWhiteSpace(newDocPath)) policy.DocumentPath = newDocPath;
        policy.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        sw.Stop();
        return ToolExecutionResult.Success(new { message = $"Policy '{policy.Title}' updated.", policy.Id, policy.Code }, RiskLevel.Medium, sw.ElapsedMilliseconds);
    }

    private async Task<ToolExecutionResult> DeletePolicyAsync(ToolExecutionContext ctx, string titleSearch, string codeSearch, Stopwatch sw)
    {
        Policy? policy = null;
        if (!string.IsNullOrWhiteSpace(codeSearch))
            policy = await _db.Policies.FirstOrDefaultAsync(p => p.Code.ToLower() == codeSearch.ToLower());
        if (policy == null && !string.IsNullOrWhiteSpace(titleSearch))
            policy = await _db.Policies.FirstOrDefaultAsync(p => p.Title.ToLower().Contains(titleSearch.ToLower()));

        if (policy == null)
            return ToolExecutionResult.Failure($"Policy '{titleSearch ?? codeSearch}' not found.", RiskLevel.Low);

        // Soft delete — mark inactive instead of physical remove
        policy.IsActive = false;
        policy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        sw.Stop();

        return ToolExecutionResult.Success(new
        {
            message = $"Policy '{policy.Title}' (Code: {policy.Code}) has been deactivated.",
            policy.Id, policy.Code
        }, RiskLevel.High, sw.ElapsedMilliseconds);
    }
}
