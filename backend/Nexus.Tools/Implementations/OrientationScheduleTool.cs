using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nexus.Data;
using Nexus.Data.Enums;
using Nexus.Data.Services;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class OrientationScheduleTool : IAgentTool
{
    private readonly IDocumentService _documentService;
    private readonly NexusDbContext _db;

    public OrientationScheduleTool(IDocumentService documentService, NexusDbContext db)
    {
        _documentService = documentService;
        _db = db;
    }

    public ToolDefinition Definition => new()
    {
        Name = "orientation.schedule",
        Description = "Generates dynamic 5-day induction and orientation schedule document for new intern cohorts or department joiners.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""department"": { ""type"": ""string"" },
            ""internCount"": { ""type"": ""integer"" }
          }
        }",
        RequiredPermission = "employee.create",
        RiskLevel = RiskLevel.Medium
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var deptName = context.GetArgument<string>("department") ?? "HR";
            var internCount = context.GetArgument<int?>("internCount") ?? 5;

            var internNames = new List<string>();
            for (int i = 1; i <= internCount; i++)
            {
                internNames.Add($"Intern #{100 + i} ({deptName})");
            }

            var doc = await _documentService.GenerateOrientationScheduleAsync(
                internNames, deptName, DateTime.UtcNow.AddDays(3), context.AgentRunId);

            sw.Stop();
            return ToolExecutionResult.Success(new
            {
                documentId = doc.Id,
                documentTitle = doc.Title,
                documentType = doc.DocumentType,
                downloadUrl = $"/api/documents/{doc.Id}/download",
                previewUrl = $"/api/documents/{doc.Id}/preview",
                message = $"Successfully generated 5-Day Orientation Schedule & Induction Plan for {internCount} interns in {deptName}."
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
