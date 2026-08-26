using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nexus.Data.Entities;

namespace Nexus.Data.Services;

public interface IDocumentService
{
    Task<GeneratedDocument> GenerateOnboardingPackageAsync(string employeeName, string departmentName, string designation, decimal salary, DateTime? startDate, Guid? runId = null);
    Task<GeneratedDocument> GenerateOffboardingPackageAsync(string employeeName, string departmentName, string designation, DateTime? effectiveDate, Guid? runId = null);
    Task<GeneratedDocument> GenerateOrientationScheduleAsync(List<string> internNames, string departmentName, DateTime? startDate, Guid? runId = null);
    Task<GeneratedDocument?> GetDocumentAsync(string documentId);
    Task<byte[]?> GetDocumentFileAsync(string documentId);
}
