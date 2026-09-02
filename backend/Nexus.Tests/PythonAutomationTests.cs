using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class PythonAutomationTests
{
    [Fact]
    public async Task WelcomeEmailTool_Executes_Python_Script_Successfully()
    {
        var pythonService = new PythonAutomationService(NullLogger<PythonAutomationService>.Instance);
        var tool = new WelcomeEmailTool(pythonService, NullLogger<WelcomeEmailTool>.Instance);

        var context = new ToolExecutionContext
        {
            ArgumentsJson = "{\"name\":\"Ahmed Khan\",\"designation\":\"Mid-Level .NET Developer\",\"department\":\"IT\"}"
        };

        var result = await tool.ExecuteAsync(context);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CreateTicketTool_Executes_Python_Script_Successfully()
    {
        var pythonService = new PythonAutomationService(NullLogger<PythonAutomationService>.Instance);
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
        var tool = new CreateTicketTool(pythonService, services, NullLogger<CreateTicketTool>.Instance);

        var context = new ToolExecutionContext
        {
            ArgumentsJson = "{\"employee\":\"Ahmed Khan\",\"department\":\"IT\",\"requestType\":\"Hardware Provisioning\"}"
        };

        var result = await tool.ExecuteAsync(context);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task ApplicationAcknowledgmentEmail_Executes_Python_Script_Successfully()
    {
        var pythonService = new PythonAutomationService(NullLogger<PythonAutomationService>.Instance);

        // Retrieve candidate record and email directly from database rather than hardcoding
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase("PythonTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;
        using var db = new NexusDbContext(options);

        var candidate = new CandidateApplication
        {
            CandidateName = "Muhammad Sufyan Khan",
            Email = "sufyan.khan@nexus.local",
            ExperienceYears = 3,
            Status = "Submitted"
        };
        db.CandidateApplications.Add(candidate);
        await db.SaveChangesAsync();

        var dbCandidate = await db.CandidateApplications.FirstAsync(c => c.CandidateName == "Muhammad Sufyan Khan");

        var jsonArgs = System.Text.Json.JsonSerializer.Serialize(new
        {
            name = dbCandidate.CandidateName,
            position = "Lead Cloud Architect",
            email = dbCandidate.Email,
            department = "IT"
        });

        var resultJson = await pythonService.ExecuteOperationAsync("email.application_acknowledgment", jsonArgs);

        Assert.NotNull(resultJson);
        Assert.Contains("email.application_acknowledgment", resultJson);
        Assert.Contains("Lead Cloud Architect", resultJson);
    }
}
