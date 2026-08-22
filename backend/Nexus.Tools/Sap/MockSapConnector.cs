using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nexus.Tools.Sap;

public class MockSapConnector : ISapConnector
{
    private readonly ILogger<MockSapConnector> _logger;

    public MockSapConnector(ILogger<MockSapConnector> logger)
    {
        _logger = logger;
    }

    public Task<SapEmployeeCreationResult> CreateEmployeeMasterDataAsync(
        string name,
        string department,
        string designation,
        decimal salary,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK SAP CONNECTOR: Simulating SAP ERP HCM master data creation for employee '{Name}' in department '{Department}'...", name, department);

        var randomId = Random.Shared.Next(1000, 9999);
        var sapEmpId = $"SAP-EMP-{DateTime.UtcNow.Year}-{randomId}";

        var result = new SapEmployeeCreationResult
        {
            MockSapEmployeeId = sapEmpId,
            Status = "SUCCESS_COMMITTED_TO_MOCK_SAP",
            ConnectorType = "MOCK SAP CONNECTOR (SIMULATED SAP ERP HCM)",
            EmployeeName = name,
            Department = department,
            Designation = designation,
            Salary = salary,
            Timestamp = DateTime.UtcNow,
            Note = "Simulated master data record created in SAP ERP HCM database. Interface architecture allows drop-in replacement with live SAP RFC/OData connectors."
        };

        return Task.FromResult(result);
    }
}
