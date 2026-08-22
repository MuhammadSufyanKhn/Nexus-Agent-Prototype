using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class BrowserAutomationTests
{
    [Fact]
    public async Task BrowserAutomationTool_Executes_Legacy_HR_Portal_Form_Submission_Successfully()
    {
        var pythonService = new PythonAutomationService(NullLogger<PythonAutomationService>.Instance);
        var tool = new BrowserAutomationTool(pythonService, NullLogger<BrowserAutomationTool>.Instance);

        var context = new ToolExecutionContext
        {
            ArgumentsJson = "{\"employeeName\":\"Ahmed Khan\",\"department\":\"IT\",\"designation\":\"Mid-Level .NET Developer\",\"salary\":68000,\"manager\":\"Tariq Mahmood\"}"
        };

        var result = await tool.ExecuteAsync(context);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        var jsonString = result.Data?.ToString() ?? string.Empty;
        Assert.Contains("HR-REC-2026-", jsonString);
        Assert.Contains("Successfully submitted employee", jsonString);
    }
}
