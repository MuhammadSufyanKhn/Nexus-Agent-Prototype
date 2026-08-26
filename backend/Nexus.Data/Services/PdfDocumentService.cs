using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.Entities;

namespace Nexus.Data.Services;

public class PdfDocumentService : IDocumentService
{
    private readonly NexusDbContext _db;
    private readonly string _documentsDir;

    public PdfDocumentService(NexusDbContext db)
    {
        _db = db;
        _documentsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "documents");
        if (!Directory.Exists(_documentsDir))
        {
            Directory.CreateDirectory(_documentsDir);
        }
    }

    public async Task<GeneratedDocument> GenerateOnboardingPackageAsync(
        string employeeName, string departmentName, string designation, decimal salary, DateTime? startDate, Guid? runId = null)
    {
        var docId = Guid.NewGuid().ToString("N");
        var effectiveDateStr = (startDate ?? DateTime.UtcNow.AddDays(7)).ToString("MMMM dd, yyyy");
        var annualSalary = salary > 10000 ? salary : salary * 12;

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>Onboarding Package - {employeeName}</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #1e293b; line-height: 1.6; background: #fff; }}
        .header {{ border-bottom: 3px solid #2563eb; padding-bottom: 20px; margin-bottom: 30px; display: flex; justify-space-between; }}
        .logo {{ font-size: 24px; font-weight: bold; color: #2563eb; letter-spacing: 1px; }}
        .doc-type {{ text-align: right; color: #64748b; font-size: 14px; text-transform: uppercase; }}
        h1 {{ color: #0f172a; font-size: 22px; margin-top: 0; }}
        h2 {{ color: #2563eb; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; margin-top: 25px; }}
        .meta-table {{ width: 100%; border-collapse: collapse; margin-bottom: 25px; background: #f8fafc; border-radius: 6px; }}
        .meta-table td {{ padding: 10px 14px; border: 1px solid #e2e8f0; font-size: 14px; }}
        .label {{ font-weight: 600; color: #475569; width: 30%; }}
        .clause {{ font-size: 13px; color: #334155; margin-bottom: 15px; text-align: justify; }}
        .checklist {{ list-style-type: none; padding-left: 0; }}
        .checklist li {{ padding: 8px 0; border-bottom: 1px dashed #e2e8f0; font-size: 14px; }}
        .signature-block {{ margin-top: 50px; display: flex; justify-content: space-between; }}
        .sig-box {{ width: 45%; border-top: 1px solid #94a3b8; paddingTop: 8px; font-size: 13px; text-align: center; color: #64748b; }}
        .badge {{ background: #dbeafe; color: #1e40af; padding: 4px 10px; border-radius: 4px; font-size: 12px; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='logo'>NEXUS ENTERPRISE HR &bull; WORKFORCE INTELLIGENCE</div>
        <div class='doc-type'>CONFIDENTIAL &bull; ONBOARDING PACKAGE</div>
    </div>

    <h1>Official Appointment & Onboarding Paperwork</h1>
    <p>This document constitutes the official employee onboarding packet and policy agreement for <strong>{employeeName}</strong>.</p>

    <h2>1. Employment Details & Compensation Schedule</h2>
    <table class='meta-table'>
        <tr><td class='label'>Employee Name</td><td><strong>{employeeName}</strong></td></tr>
        <tr><td class='label'>Assigned Department</td><td><span class='badge'>{departmentName}</span></td></tr>
        <tr><td class='label'>Designation / Role</td><td>{designation}</td></tr>
        <tr><td class='label'>Annual Compensation</td><td><strong>${annualSalary:N2} USD / Year</strong> (${(annualSalary / 12):N2}/mo)</td></tr>
        <tr><td class='label'>Effective Joining Date</td><td>{effectiveDateStr}</td></tr>
        <tr><td class='label'>Employment Status</td><td>Full-Time Regular Employee</td></tr>
    </table>

    <h2>2. Non-Disclosure & Confidentiality Agreement (NDA)</h2>
    <div class='clause'>
        The employee agrees to maintain strict confidentiality regarding all proprietary source code, internal system access credentials, customer data, and trade secrets belonging to Nexus Enterprise Operations during and after their tenure.
    </div>

    <h2>3. IT Asset & System Security Provisioning</h2>
    <ul class='checklist'>
        <li>&#9744; Provisioning of Enterprise Workstation Laptop (Core i9 / 32GB RAM)</li>
        <li>&#9744; Active Directory & LDAP Enterprise Credentials Generation</li>
        <li>&#9744; Security Badge & Access Control Card Issuance</li>
        <li>&#9744; Software Licensing Setup (Visual Studio Pro, Slack, Microsoft 365)</li>
    </ul>

    <h2>4. HR Policy Acknowledgement</h2>
    <div class='clause'>
        By signing below, the employee acknowledges receipt of Company Policy POL-HR-001 and agrees to abide by the workplace standards, cybersecurity protocols, and expense compliance rules of the organization.
    </div>

    <div class='signature-block' style='margin-top:60px;'>
        <div class='sig-box'>Authorized HR Representative Signature<br/><strong>Human Resources Department</strong></div>
        <div class='sig-box'>Employee Acceptance Signature<br/><strong>{employeeName}</strong></div>
    </div>
</body>
</html>";

        var filePath = Path.Combine(_documentsDir, $"{docId}.html");
        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);

        var doc = new GeneratedDocument
        {
            Id = docId,
            DocumentType = "ONBOARDING_PACKAGE",
            Title = $"Onboarding Paperwork - {employeeName} ({departmentName})",
            FilePath = filePath,
            ContentHtml = html,
            EmployeeName = employeeName,
            DepartmentName = departmentName,
            AgentRunId = runId,
            CreatedAt = DateTime.UtcNow
        };

        return doc;
    }

    public async Task<GeneratedDocument> GenerateOffboardingPackageAsync(
        string employeeName, string departmentName, string designation, DateTime? effectiveDate, Guid? runId = null)
    {
        var docId = Guid.NewGuid().ToString("N");
        var effDateStr = (effectiveDate ?? DateTime.UtcNow).ToString("MMMM dd, yyyy");

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>Offboarding Package - {employeeName}</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #1e293b; line-height: 1.6; background: #fff; }}
        .header {{ border-bottom: 3px solid #ef4444; padding-bottom: 20px; margin-bottom: 30px; display: flex; justify-space-between; }}
        .logo {{ font-size: 24px; font-weight: bold; color: #ef4444; letter-spacing: 1px; }}
        .doc-type {{ text-align: right; color: #64748b; font-size: 14px; text-transform: uppercase; }}
        h1 {{ color: #0f172a; font-size: 22px; margin-top: 0; }}
        h2 {{ color: #ef4444; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; margin-top: 25px; }}
        .meta-table {{ width: 100%; border-collapse: collapse; margin-bottom: 25px; background: #f8fafc; border-radius: 6px; }}
        .meta-table td {{ padding: 10px 14px; border: 1px solid #e2e8f0; font-size: 14px; }}
        .label {{ font-weight: 600; color: #475569; width: 30%; }}
        .checklist {{ list-style-type: none; padding-left: 0; }}
        .checklist li {{ padding: 8px 0; border-bottom: 1px dashed #e2e8f0; font-size: 14px; }}
        .signature-block {{ margin-top: 50px; display: flex; justify-space-between; }}
        .sig-box {{ width: 45%; border-top: 1px solid #94a3b8; paddingTop: 8px; font-size: 13px; text-align: center; color: #64748b; }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='logo'>NEXUS ENTERPRISE HR &bull; WORKFORCE INTELLIGENCE</div>
        <div class='doc-type'>CONFIDENTIAL &bull; EXIT CLEARANCE PACKAGE</div>
    </div>

    <h1>Employee Offboarding & Exit Clearance Packet</h1>
    <p>This document records the formal initiation of offboarding and clearance for <strong>{employeeName}</strong>.</p>

    <h2>1. Separation Record Summary</h2>
    <table class='meta-table'>
        <tr><td class='label'>Employee Name</td><td><strong>{employeeName}</strong></td></tr>
        <tr><td class='label'>Department</td><td>{departmentName}</td></tr>
        <tr><td class='label'>Role / Title</td><td>{designation}</td></tr>
        <tr><td class='label'>Effective Separation Date</td><td><strong>{effDateStr}</strong></td></tr>
        <tr><td class='label'>Offboarding Status</td><td>Clearance Workflow Initiated</td></tr>
    </table>

    <h2>2. Asset & IT Revocation Checklist</h2>
    <ul class='checklist'>
        <li>&#9744; Hardware Asset Return (Laptop, Monitor, Security Token)</li>
        <li>&#9744; Active Directory & SSO Credentials Revocation</li>
        <li>&#9744; Email & Slack Account Suspension</li>
        <li>&#9744; Building Access Pass Deactivation</li>
    </ul>

    <h2>3. Final Settlement & Payroll Reconciliation</h2>
    <p style='font-size:13px;'>Final paycheck including accrued PTO payout will be processed following full HR and IT departmental clearance.</p>

    <div class='signature-block' style='margin-top:60px;'>
        <div class='sig-box'>HR Operations Lead Signature<br/><strong>Human Resources Department</strong></div>
        <div class='sig-box'>Departing Employee Signature<br/><strong>{employeeName}</strong></div>
    </div>
</body>
</html>";

        var filePath = Path.Combine(_documentsDir, $"{docId}.html");
        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);

        var doc = new GeneratedDocument
        {
            Id = docId,
            DocumentType = "OFFBOARDING_PACKAGE",
            Title = $"Offboarding Exit Packet - {employeeName} ({departmentName})",
            FilePath = filePath,
            ContentHtml = html,
            EmployeeName = employeeName,
            DepartmentName = departmentName,
            AgentRunId = runId,
            CreatedAt = DateTime.UtcNow
        };

        return doc;
    }

    public async Task<GeneratedDocument> GenerateOrientationScheduleAsync(
        List<string> internNames, string departmentName, DateTime? startDate, Guid? runId = null)
    {
        var docId = Guid.NewGuid().ToString("N");
        var joinDate = startDate ?? DateTime.UtcNow.AddDays(5);
        var joinStr = joinDate.ToString("MMMM dd, yyyy");
        var internListHtml = string.Join("", internNames.Select(n => $"<li><strong>{n}</strong> - {departmentName} Intern</li>"));

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>Orientation Schedule - {departmentName} Interns</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #1e293b; line-height: 1.6; background: #fff; }}
        .header {{ border-bottom: 3px solid #10b981; padding-bottom: 20px; margin-bottom: 30px; display: flex; justify-space-between; }}
        .logo {{ font-size: 24px; font-weight: bold; color: #10b981; letter-spacing: 1px; }}
        .doc-type {{ text-align: right; color: #64748b; font-size: 14px; text-transform: uppercase; }}
        h1 {{ color: #0f172a; font-size: 22px; margin-top: 0; }}
        h2 {{ color: #059669; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; margin-top: 25px; }}
        .schedule-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
        .schedule-table th, .schedule-table td {{ padding: 10px 14px; border: 1px solid #cbd5e1; text-align: left; font-size: 13px; }}
        .schedule-table th {{ background: #f1f5f9; color: #334155; font-weight: bold; }}
        .intern-box {{ background: #ecfdf5; border-left: 4px solid #10b981; padding: 12px; margin-bottom: 20px; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='logo'>NEXUS ENTERPRISE HR &bull; WORKFORCE INTELLIGENCE</div>
        <div class='doc-type'>ORIENTATION & INDUCTION PLAN</div>
    </div>

    <h1>5-Day Workforce Orientation & Induction Schedule</h1>
    <p>Comprehensive onboarding schedule for incoming cohort in the <strong>{departmentName}</strong> department commencing <strong>{joinStr}</strong>.</p>

    <div class='intern-box'>
        <strong>Participating Intern Cohort ({internNames.Count}):</strong>
        <ul style='margin-bottom:0;'>{internListHtml}</ul>
    </div>

    <h2>Day-by-Day Induction Plan</h2>
    <table class='schedule-table'>
        <thead>
            <tr><th>Day / Time</th><th>Activity & Session Title</th><th>Facilitator / Target</th></tr>
        </thead>
        <tbody>
            <tr><td><strong>Day 1 &bull; 09:00 AM</strong></td><td>Welcome & Executive Keynote Introduction</td><td>HR Operations Lead</td></tr>
            <tr><td><strong>Day 1 &bull; 11:00 AM</strong></td><td>IT Credentialing & Workstation Setup</td><td>IT Infrastructure Team</td></tr>
            <tr><td><strong>Day 2 &bull; 10:00 AM</strong></td><td>Company Policy & Cybersecurity Compliance Overview</td><td>Legal & Security Team</td></tr>
            <tr><td><strong>Day 3 &bull; 02:00 PM</strong></td><td>{departmentName} Departmental Architecture & Workflow Deep Dive</td><td>Department Head ({departmentName})</td></tr>
            <tr><td><strong>Day 4 &bull; 11:00 AM</strong></td><td>System Tools & Portal Hands-on Workshop</td><td>Senior Tech Lead</td></tr>
            <tr><td><strong>Day 5 &bull; 03:00 PM</strong></td><td>Week 1 Review & Buddy Assignment Meeting</td><td>Talent Management</td></tr>
        </tbody>
    </table>
</body>
</html>";

        var filePath = Path.Combine(_documentsDir, $"{docId}.html");
        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);

        var doc = new GeneratedDocument
        {
            Id = docId,
            DocumentType = "ORIENTATION_SCHEDULE",
            Title = $"Orientation Schedule - {departmentName} Interns ({internNames.Count})",
            FilePath = filePath,
            ContentHtml = html,
            DepartmentName = departmentName,
            AgentRunId = runId,
            CreatedAt = DateTime.UtcNow
        };

        return doc;
    }

    public async Task<GeneratedDocument?> GetDocumentAsync(string documentId)
    {
        var filePath = Path.Combine(_documentsDir, $"{documentId}.html");
        if (!File.Exists(filePath)) return null;

        var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        return new GeneratedDocument
        {
            Id = documentId,
            FilePath = filePath,
            ContentHtml = html
        };
    }

    public async Task<byte[]?> GetDocumentFileAsync(string documentId)
    {
        var filePath = Path.Combine(_documentsDir, $"{documentId}.html");
        if (!File.Exists(filePath)) return null;
        return await File.ReadAllBytesAsync(filePath);
    }
}
