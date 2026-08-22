using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Nexus.Tools.Sap;
using Xunit;

namespace Nexus.Tests;

public class SapConnectorTests
{
    [Fact]
    public async Task MockSapConnector_Creates_Master_Data_Successfully()
    {
        var connector = new MockSapConnector(NullLogger<MockSapConnector>.Instance);
        var result = await connector.CreateEmployeeMasterDataAsync("Ahmed Khan", "IT", "Mid-Level .NET Developer", 68000.00m);

        Assert.NotNull(result);
        Assert.Equal("SUCCESS_COMMITTED_TO_MOCK_SAP", result.Status);
        Assert.Contains("SAP-EMP-2026-", result.MockSapEmployeeId);
        Assert.Contains("MOCK SAP CONNECTOR", result.ConnectorType);
        Assert.Equal("Ahmed Khan", result.EmployeeName);
        Assert.Equal("IT", result.Department);
    }

    [Fact]
    public async Task MockSapTool_Executes_SapEmployeeCreate_Tool_Successfully()
    {
        var connector = new MockSapConnector(NullLogger<MockSapConnector>.Instance);
        var tool = new MockSapTool(connector, NullLogger<MockSapTool>.Instance);

        var context = new ToolExecutionContext
        {
            ArgumentsJson = "{\"name\":\"Ahmed Khan\",\"department\":\"IT\",\"designation\":\"Mid-Level .NET Developer\",\"salary\":68000}"
        };

        var result = await tool.ExecuteAsync(context);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        var sapRes = result.Data as SapEmployeeCreationResult;
        Assert.NotNull(sapRes);
        Assert.Contains("SAP-EMP-", sapRes!.MockSapEmployeeId);
        Assert.Contains("MOCK SAP CONNECTOR", sapRes.ConnectorType);
    }

    [Fact]
    public async Task MockSapTool_Verifies_Clear_Mock_Identification()
    {
        var connector = new MockSapConnector(NullLogger<MockSapConnector>.Instance);
        var tool = new MockSapTool(connector, NullLogger<MockSapTool>.Instance);

        var context = new ToolExecutionContext
        {
            ArgumentsJson = "{\"name\":\"Sara Ali\",\"department\":\"HR\"}"
        };

        var result = await tool.ExecuteAsync(context);
        var sapRes = result.Data as SapEmployeeCreationResult;

        Assert.NotNull(sapRes);
        Assert.Contains("MOCK SAP CONNECTOR", sapRes!.ConnectorType);
        Assert.Contains("Simulated master data record created in SAP ERP HCM database", sapRes.Note);
    }
}
