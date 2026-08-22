using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Tools.Sap;

public class SapEmployeeCreationResult
{
    public string MockSapEmployeeId { get; set; } = string.Empty;
    public string Status { get; set; } = "SUCCESS_COMMITTED_TO_MOCK_SAP";
    public string ConnectorType { get; set; } = "MOCK SAP CONNECTOR (SIMULATED SAP ERP HCM)";
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Note { get; set; } = "Simulated master data record created in SAP ERP HCM database. Interfaces cleanly with real SAP RFC/OData connectors.";
}

public interface ISapConnector
{
    Task<SapEmployeeCreationResult> CreateEmployeeMasterDataAsync(
        string name,
        string department,
        string designation,
        decimal salary,
        CancellationToken cancellationToken = default);
}
