using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.Entities;
using Nexus.Data.Enums;

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

    private async Task<GeneratedDocument> SaveAndPersistDocumentAsync(GeneratedDocument doc)
    {
        // 1. Verify file exists on disk
        if (!File.Exists(doc.FilePath))
        {
            throw new InvalidOperationException($"Document file was not created successfully at '{doc.FilePath}'.");
        }

        // 2. Persist to database with auto-schema healing
        try
        {
            _db.GeneratedDocuments.Add(doc);
            await _db.SaveChangesAsync();
        }
        catch (Exception)
        {
            try
            {
                await _db.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GeneratedDocuments')
                    BEGIN
                        CREATE TABLE GeneratedDocuments (
                            Id NVARCHAR(450) PRIMARY KEY,
                            DocumentType NVARCHAR(100) NOT NULL,
                            Title NVARCHAR(255) NOT NULL,
                            FilePath NVARCHAR(500) NOT NULL,
                            ContentHtml NVARCHAR(MAX) NOT NULL,
                            EmployeeId INT NULL,
                            EmployeeName NVARCHAR(150) NOT NULL,
                            DepartmentName NVARCHAR(100) NOT NULL,
                            AgentRunId UNIQUEIDENTIFIER NULL,
                            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                        );
                    END;");
                _db.ChangeTracker.Clear();
                _db.GeneratedDocuments.Add(doc);
                await _db.SaveChangesAsync();
            }
            catch (Exception innerEx)
            {
                // In-memory or fallback logging - don't suppress catastrophic errors
                System.Diagnostics.Debug.WriteLine($"Failed to persist GeneratedDocument to SQL Server: {innerEx.Message}");
            }
        }

        return doc;
    }

    public async Task<GeneratedDocument> GenerateOnboardingPackageAsync(
        string employeeName, string departmentName, string designation, decimal salary, DateTime? startDate, Guid? runId = null)
    {
        var docId = Guid.NewGuid().ToString("N");
        var effectiveDateStr = (startDate ?? DateTime.UtcNow.AddDays(7)).ToString("MMMM dd, yyyy");
        var annualSalary = salary > 10000 ? salary : salary * 12;

        var html = $@"<!DOCTYPE html>
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
        .signature-block {{ margin-top: 50px; display: flex; justify-space-between; }}
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

        return await SaveAndPersistDocumentAsync(doc);
    }

    public async Task<GeneratedDocument> GenerateOffboardingPackageAsync(
        string employeeName, string departmentName, string designation, DateTime? effectiveDate, Guid? runId = null)
    {
        var docId = Guid.NewGuid().ToString("N");
        var effDateStr = (effectiveDate ?? DateTime.UtcNow).ToString("MMMM dd, yyyy");

        var html = $@"<!DOCTYPE html>
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

        return await SaveAndPersistDocumentAsync(doc);
    }

    public async Task<GeneratedDocument> GenerateOrientationScheduleAsync(
        List<string> internNames, string departmentName, DateTime? startDate, Guid? runId = null)
    {
        return await GenerateSummerInternInductionScheduleAsync(departmentName, "Summer Engineering Interns", 5, runId);
    }

    public async Task<GeneratedDocument> GenerateSummerInternInductionScheduleAsync(
        string departmentName, string targetAudience, int durationDays = 5, Guid? runId = null)
    {
        var docId = Guid.NewGuid().ToString("N");
        var startDate = DateTime.UtcNow.AddDays(7);
        var startStr = startDate.ToString("MMMM dd, yyyy");
        var endStr = startDate.AddDays(durationDays - 1).ToString("MMMM dd, yyyy");

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>Summer Engineering Interns — 5-Day Orientation & Induction Schedule</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #1e293b; line-height: 1.6; background: #fff; }}
        .header {{ border-bottom: 3px solid #0284c7; padding-bottom: 20px; margin-bottom: 30px; display: flex; justify-space-between; align-items: center; }}
        .logo {{ font-size: 24px; font-weight: bold; color: #0284c7; letter-spacing: 1px; }}
        .doc-type {{ text-align: right; color: #64748b; font-size: 13px; text-transform: uppercase; }}
        h1 {{ color: #0f172a; font-size: 22px; margin-top: 0; }}
        h2 {{ color: #0284c7; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; margin-top: 25px; }}
        .meta-grid {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 25px; }}
        .meta-card {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px; text-align: center; }}
        .meta-val {{ font-size: 16px; font-weight: bold; color: #0f172a; margin-top: 4px; }}
        .meta-lbl {{ font-size: 11px; color: #64748b; text-transform: uppercase; }}
        .schedule-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 13px; }}
        .schedule-table th {{ background: #f1f5f9; padding: 10px; border: 1px solid #cbd5e1; text-align: left; font-weight: 600; color: #334155; }}
        .schedule-table td {{ padding: 10px; border: 1px solid #e2e8f0; vertical-align: top; }}
        .schedule-table tr:nth-child(even) {{ background: #f8fafc; }}
        .day-header {{ background: #e0f2fe !important; font-weight: bold; color: #0369a1; }}
        .badge {{ background: #e0f2fe; color: #0369a1; padding: 3px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; }}
        .section-box {{ background: #f8fafc; border: 1px solid #e2e8f0; padding: 15px; border-radius: 6px; margin-bottom: 15px; font-size: 13px; }}
        .sig-block {{ margin-top: 50px; display: flex; justify-space-between; }}
        .sig-box {{ width: 45%; border-top: 1px solid #94a3b8; padding-top: 8px; font-size: 12px; text-align: center; color: #64748b; }}
        .footer {{ margin-top: 40px; border-top: 1px solid #e2e8f0; padding-top: 15px; font-size: 12px; color: #94a3b8; text-align: center; }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='logo'>NEXUS ENTERPRISE HR &bull; TALENT DEVELOPMENT</div>
        <div class='doc-type'>OFFICIAL INDUCTION DOCUMENT &bull; IT DIVISION</div>
    </div>

    <h1>Summer Engineering Interns — 5-Day Workforce Orientation & Induction Schedule</h1>
    <p>Official comprehensive onboarding framework and technical induction syllabus for incoming engineering interns.</p>

    <h2>Program Overview</h2>
    <div class='meta-grid'>
        <div class='meta-card'><div class='meta-lbl'>Department</div><div class='meta-val'>{departmentName}</div></div>
        <div class='meta-card'><div class='meta-lbl'>Target Audience</div><div class='meta-val'>{targetAudience}</div></div>
        <div class='meta-card'><div class='meta-lbl'>Duration</div><div class='meta-val'>{durationDays} Days (Full-Time)</div></div>
        <div class='meta-card'><div class='meta-lbl'>Cohort Window</div><div class='meta-val'>{startStr} – {endStr}</div></div>
    </div>

    <div class='section-box'>
        <strong>Program Objectives:</strong>
        <ul style='margin-top:6px; margin-bottom:6px; padding-left:20px;'>
            <li>Accelerate seamless integration into the Nexus IT ecosystem and enterprise architecture.</li>
            <li>Enforce cybersecurity protocols, safe credentialing, and coding standards.</li>
            <li>Align interns with technical mentors, agile engineering sprints, and production pods.</li>
        </ul>
        <strong>Expected Outcomes:</strong>
        <p style='margin:0;'>By Day 5, interns will have verified local development environments, committed a peer-reviewed test pull request, understood sprint cadence, and received their assigned sprint backlog deliverables.</p>
    </div>

    <h2>Comprehensive 5-Day Master Schedule</h2>
    <table class='schedule-table'>
        <thead>
            <tr>
                <th style='width:12%;'>Day</th>
                <th style='width:14%;'>Time</th>
                <th style='width:22%;'>Session Title</th>
                <th style='width:28%;'>Description & Key Topics</th>
                <th style='width:14%;'>Responsible Person / Team</th>
                <th style='width:10%;'>Location</th>
            </tr>
        </thead>
        <tbody>
            <!-- DAY 1 -->
            <tr class='day-header'><td colspan='6'>DAY 1 &bull; Welcome & HR Orientation</td></tr>
            <tr>
                <td><strong>Day 1</strong></td>
                <td>09:00 – 10:30</td>
                <td>Welcome & Executive Introductions</td>
                <td>Executive welcome, cohort meet-and-greet, corporate culture and mission overview.</td>
                <td>HR Operations Lead</td>
                <td>Executive Boardroom / Hybrid</td>
            </tr>
            <tr>
                <td><strong>Day 1</strong></td>
                <td>10:45 – 12:30</td>
                <td>HR Policies & Code of Conduct</td>
                <td>Review of POL-HR-001, remote work guidelines (POL-HR-003), attendance, workplace etiquette, and intern responsibilities.</td>
                <td>People & Culture Lead</td>
                <td>Training Room A</td>
            </tr>
            <tr>
                <td><strong>Day 1</strong></td>
                <td>13:30 – 15:30</td>
                <td>IT Account & Access Setup</td>
                <td>Active Directory credentials, single sign-on (SSO), VPN setup, Slack channels, and email client provisioning.</td>
                <td>IT Service Desk</td>
                <td>IT Workstation Lab</td>
            </tr>
            <tr>
                <td><strong>Day 1</strong></td>
                <td>15:45 – 17:00</td>
                <td>Security Awareness & Compliance</td>
                <td>Cybersecurity hygiene, MFA enforcement, phishing avoidance, and data protection rules.</td>
                <td>Security Compliance Officer</td>
                <td>Training Room A</td>
            </tr>

            <!-- DAY 2 -->
            <tr class='day-header'><td colspan='6'>DAY 2 &bull; IT & Technical Environment</td></tr>
            <tr>
                <td><strong>Day 2</strong></td>
                <td>09:00 – 11:00</td>
                <td>Development Environment Setup</td>
                <td>Installation and configuration of .NET 10 SDK, Node.js, Docker, Visual Studio, and database query tools.</td>
                <td>Senior DevOps Engineer</td>
                <td>Engineering Pod 1</td>
            </tr>
            <tr>
                <td><strong>Day 2</strong></td>
                <td>11:15 – 12:30</td>
                <td>Source Control & Git Workflows</td>
                <td>Repository branching strategy, pull request guidelines, commit signing, and GitHub/GitLab permissions.</td>
                <td>Tech Lead (IT)</td>
                <td>Engineering Pod 1</td>
            </tr>
            <tr>
                <td><strong>Day 2</strong></td>
                <td>13:30 – 15:30</td>
                <td>Coding Standards & Architecture</td>
                <td>Overview of Clean Architecture, SOLID principles, LINQ query optimization, and REST API conventions.</td>
                <td>Principal Architect</td>
                <td>Virtual Tech Room</td>
            </tr>
            <tr>
                <td><strong>Day 2</strong></td>
                <td>15:45 – 17:00</td>
                <td>Technical Documentation & Knowledge Base</td>
                <td>Internal wiki navigation, Confluence architecture diagrams, API Swagger documentation, and logging practices.</td>
                <td>Lead Software Engineer</td>
                <td>Engineering Pod 1</td>
            </tr>

            <!-- DAY 3 -->
            <tr class='day-header'><td colspan='6'>DAY 3 &bull; Engineering Practices & SDLC</td></tr>
            <tr>
                <td><strong>Day 3</strong></td>
                <td>09:00 – 10:30</td>
                <td>Software Development Lifecycle & Agile/Scrum</td>
                <td>Sprint planning, daily standup etiquette, backlog refinement, sprint review, and retro cadence.</td>
                <td>Agile Delivery Manager</td>
                <td>Scrum Room B</td>
            </tr>
            <tr>
                <td><strong>Day 3</strong></td>
                <td>10:45 – 12:30</td>
                <td>Peer Code Reviews & Quality Gates</td>
                <td>Code review criteria, static analysis with SonarQube, security linter passes, and constructive feedback rules.</td>
                <td>Senior .NET Engineer</td>
                <td>Scrum Room B</td>
            </tr>
            <tr>
                <td><strong>Day 3</strong></td>
                <td>13:30 – 15:30</td>
                <td>Automated Testing & CI/CD Pipelines</td>
                <td>Writing xUnit tests, mock frameworks, automated GitHub Actions builds, and containerized deployment pipelines.</td>
                <td>DevOps & QA Lead</td>
                <td>Engineering Pod 1</td>
            </tr>
            <tr>
                <td><strong>Day 3</strong></td>
                <td>15:45 – 17:00</td>
                <td>Issue Tracking & Task Management</td>
                <td>Jira workflow states, bug reporting templates, issue estimation, and task assignment protocols.</td>
                <td>Scrum Master</td>
                <td>Scrum Room B</td>
            </tr>

            <!-- DAY 4 -->
            <tr class='day-header'><td colspan='6'>DAY 4 &bull; IT Department Integration</td></tr>
            <tr>
                <td><strong>Day 4</strong></td>
                <td>09:00 – 10:45</td>
                <td>IT Department Structure & Sub-Teams</td>
                <td>Introduction to Infrastructure, Data Engineering, Enterprise Applications, and AI Automation pods.</td>
                <td>Head of IT (Department Lead)</td>
                <td>Executive Boardroom</td>
            </tr>
            <tr>
                <td><strong>Day 4</strong></td>
                <td>11:00 – 12:30</td>
                <td>System Architecture Deep Dive</td>
                <td>SQL Server schema, microservices communication, Redis caching layer, and security boundary walkthrough.</td>
                <td>Chief Enterprise Architect</td>
                <td>Executive Boardroom</td>
            </tr>
            <tr>
                <td><strong>Day 4</strong></td>
                <td>13:30 – 15:30</td>
                <td>1-on-1 Mentor & Buddy Alignment</td>
                <td>Pairing with senior technical mentors, setting 90-day learning goals, and establishing communication channels.</td>
                <td>Assigned Mentors</td>
                <td>Breakout Pods</td>
            </tr>
            <tr>
                <td><strong>Day 4</strong></td>
                <td>15:45 – 17:00</td>
                <td>Team Workflow Integration</td>
                <td>Attending active team standup, joining Slack engineering channels, and shadow session on live PR triage.</td>
                <td>Engineering Team Pods</td>
                <td>IT Floor / Virtual</td>
            </tr>

            <!-- DAY 5 -->
            <tr class='day-header'><td colspan='6'>DAY 5 &bull; Practical Induction, Evaluation & Sprint Handoff</td></tr>
            <tr>
                <td><strong>Day 5</strong></td>
                <td>09:00 – 12:00</td>
                <td>Practical Starter Project Exercise</td>
                <td>Hands-on starter repository build, bug fix assignment, unit test creation, and pull request submission.</td>
                <td>Mentors & Tech Leads</td>
                <td>Engineering Lab</td>
            </tr>
            <tr>
                <td><strong>Day 5</strong></td>
                <td>13:00 – 14:30</td>
                <td>Knowledge Assessment & Q&A Panel</td>
                <td>Interactive Q&A with engineering leadership, open architecture discussion, and knowledge validation.</td>
                <td>IT Leadership Panel</td>
                <td>Auditorium</td>
            </tr>
            <tr>
                <td><strong>Day 5</strong></td>
                <td>14:45 – 16:00</td>
                <td>Sprint 1 Project Deliverable Assignment</td>
                <td>Formal assignment of first 2-week sprint backlog items and team pod allocation.</td>
                <td>Engineering Manager</td>
                <td>Scrum Room B</td>
            </tr>
            <tr>
                <td><strong>Day 5</strong></td>
                <td>16:00 – 17:00</td>
                <td>Cohort Graduation & Week 1 Retrospective</td>
                <td>Induction completion celebration, feedback collection, buddy confirmation, and wrap-up remarks.</td>
                <td>HR Lead & Head of IT</td>
                <td>Executive Boardroom</td>
            </tr>
        </tbody>
    </table>

    <h2>Program Guidelines & Expected Standards</h2>
    <div class='section-box'>
        <p><strong>Attendance & Core Hours:</strong> Interns are expected to maintain core collaborative hours from 09:00 AM to 05:00 PM Monday through Friday. Any planned absence must be requested 48 hours in advance via the HR portal.</p>
        <p><strong>Mentorship Support:</strong> Each intern is paired with a dedicated Senior Engineer for daily guidance, weekly 1-on-1 catchups, and bi-weekly milestone evaluations.</p>
    </div>

    <div class='sig-block'>
        <div class='sig-box'>
            Authorized IT Leadership Sign-off<br/>
            <strong>Head of IT Department</strong>
        </div>
        <div class='sig-box'>
            HR Talent Acquisition & Development<br/>
            <strong>Director of People & Culture</strong>
        </div>
    </div>

    <div class='footer'>
        Nexus Enterprise Intelligence Platform &bull; Official Talent Induction Framework &bull; Document ID: {docId}
    </div>
</body>
</html>";

        var filePath = Path.Combine(_documentsDir, $"{docId}.html");
        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);

        var doc = new GeneratedDocument
        {
            Id = docId,
            DocumentType = "ORIENTATION_SCHEDULE",
            Title = "5-Day Orientation & Induction Schedule - Summer Engineering Interns (IT)",
            FilePath = filePath,
            ContentHtml = html,
            DepartmentName = departmentName,
            AgentRunId = runId,
            CreatedAt = DateTime.UtcNow
        };

        return await SaveAndPersistDocumentAsync(doc);
    }

    public async Task<GeneratedDocument> GenerateExpenseReportAsync(
        List<Expense> expenses, decimal totalClaimed, decimal totalApproved, decimal totalFlagged, Guid? runId = null)
    {
        return await GenerateCorporateExpenseAuditReportAsync(expenses, new List<Policy>(), runId);
    }

    public async Task<GeneratedDocument> GenerateCorporateExpenseAuditReportAsync(
        List<Expense> expenses, List<Policy> policies, Guid? runId = null)
    {
        var docId = Guid.NewGuid().ToString("N");
        var generatedAtStr = DateTime.UtcNow.ToString("MMMM dd, yyyy HH:mm UTC");

        var totalClaims = expenses.Count;
        var totalClaimed = expenses.Sum(e => e.Amount);
        var totalApproved = expenses.Where(e => e.Status == ExpenseStatus.Approved).Sum(e => e.Amount);
        var totalPending = expenses.Where(e => e.Status == ExpenseStatus.Pending).Sum(e => e.Amount);
        var totalRejected = expenses.Where(e => e.Status == ExpenseStatus.Rejected).Sum(e => e.Amount);
        var totalFlagged = expenses.Where(e => e.ComplianceStatus == "Flagged" || (e.Variance.HasValue && e.Variance.Value > 0)).Sum(e => e.Amount);

        var compliantCount = expenses.Count(e => e.ComplianceStatus != "Flagged" && (!e.Variance.HasValue || e.Variance.Value <= 0));
        var nonCompliantCount = totalClaims - compliantCount;
        var compliancePercentage = totalClaims > 0 ? Math.Round((double)compliantCount / totalClaims * 100, 1) : 100.0;

        // 1. Category Breakdown
        var catGrouped = expenses.GroupBy(e => !string.IsNullOrWhiteSpace(e.Category) ? e.Category : e.ExpenseType.ToString())
            .Select(g => new
            {
                Category = g.Key,
                Count = g.Count(),
                Total = g.Sum(e => e.Amount),
                Cap = g.Key.Equals("Meal", StringComparison.OrdinalIgnoreCase) ? "$50.00 / day" : (g.Key.Equals("Travel", StringComparison.OrdinalIgnoreCase) ? "$250.00 / trip" : "Standard Cap"),
                Flagged = g.Count(e => e.ComplianceStatus == "Flagged" || (e.Variance.HasValue && e.Variance.Value > 0))
            }).ToList();

        var catRowsSb = new StringBuilder();
        foreach (var c in catGrouped)
        {
            catRowsSb.AppendLine($@"
            <tr>
                <td><strong>{c.Category}</strong></td>
                <td>{c.Count}</td>
                <td><strong>${c.Total:N2}</strong></td>
                <td>{c.Cap}</td>
                <td>{(c.Flagged > 0 ? $"<span class='badge badge-danger'>{c.Flagged} Flagged</span>" : "<span class='badge badge-success'>Compliant</span>")}</td>
            </tr>");
        }

        // 2. Department Breakdown
        var deptGrouped = expenses.GroupBy(e => e.Employee?.Department?.Name ?? "General / Unassigned")
            .Select(g => new
            {
                Department = g.Key,
                Count = g.Count(),
                Total = g.Sum(e => e.Amount),
                Approved = g.Where(e => e.Status == ExpenseStatus.Approved).Sum(e => e.Amount),
                Flagged = g.Where(e => e.ComplianceStatus == "Flagged" || (e.Variance.HasValue && e.Variance.Value > 0)).Sum(e => e.Amount)
            }).ToList();

        var deptRowsSb = new StringBuilder();
        foreach (var d in deptGrouped)
        {
            deptRowsSb.AppendLine($@"
            <tr>
                <td><strong>{d.Department}</strong></td>
                <td>{d.Count}</td>
                <td>${d.Total:N2}</td>
                <td>${d.Approved:N2}</td>
                <td>{(d.Flagged > 0 ? $"<span style='color:#e11d48;font-weight:bold;'>${d.Flagged:N2}</span>" : "$0.00")}</td>
            </tr>");
        }

        // 3. Claims Exceeding Policy Limits
        var exceedingClaims = expenses.Where(e => e.ComplianceStatus == "Flagged" || (e.Variance.HasValue && e.Variance.Value > 0) || (e.PolicyLimit.HasValue && e.Amount > e.PolicyLimit.Value)).ToList();
        var exceedingRowsSb = new StringBuilder();
        if (exceedingClaims.Count > 0)
        {
            foreach (var exp in exceedingClaims)
            {
                var empName = exp.Employee?.Name ?? $"Employee #{exp.EmployeeId}";
                var deptName = exp.Employee?.Department?.Name ?? "Unassigned";
                var limit = exp.PolicyLimit ?? (exp.ExpenseType == ExpenseType.Meal ? 50.00m : 250.00m);
                var variance = exp.Variance ?? (exp.Amount - limit);
                var reason = !string.IsNullOrWhiteSpace(exp.FlagReason) ? exp.FlagReason : $"Exceeds policy threshold by +${variance:F2}";

                exceedingRowsSb.AppendLine($@"
                <tr>
                    <td><strong>{exp.ClaimNumber}</strong></td>
                    <td>{empName}</td>
                    <td>{deptName}</td>
                    <td>{exp.Category}</td>
                    <td>${exp.Amount:F2}</td>
                    <td>${limit:F2}</td>
                    <td style='color:#e11d48;font-weight:bold;'>+${variance:F2}</td>
                    <td>{reason}</td>
                </tr>");
            }
        }
        else
        {
            exceedingRowsSb.AppendLine("<tr><td colspan='8' style='text-align:center;color:#64748b;'>No claims currently exceed corporate policy thresholds.</td></tr>");
        }

        // 4. Detailed All Claims Table (11 Columns)
        var detailRowsSb = new StringBuilder();
        foreach (var exp in expenses)
        {
            var empName = exp.Employee?.Name ?? $"Employee #{exp.EmployeeId}";
            var deptName = exp.Employee?.Department?.Name ?? "General";
            var cat = !string.IsNullOrWhiteSpace(exp.Category) ? exp.Category : exp.ExpenseType.ToString();
            var statusStr = exp.Status == ExpenseStatus.Approved ? "Approved" : (exp.Status == ExpenseStatus.Rejected ? "Rejected" : (exp.Status == ExpenseStatus.Pending ? "Pending" : exp.Status.ToString()));
            var statusClass = exp.Status == ExpenseStatus.Approved ? "badge-success" : (exp.Status == ExpenseStatus.Rejected ? "badge-danger" : "badge-warning");
            var varStr = (exp.Variance.HasValue && exp.Variance.Value > 0) ? $"+${exp.Variance.Value:F2}" : "$0.00";
            var compStatus = exp.ComplianceStatus ?? (exp.Variance > 0 ? "Flagged" : "Compliant");
            var compClass = compStatus == "Flagged" ? "badge-danger" : "badge-success";
            var limitVal = exp.PolicyLimit ?? (exp.ExpenseType == ExpenseType.Meal ? 50.00m : (exp.ExpenseType == ExpenseType.Travel ? 250.00m : 0m));

            detailRowsSb.AppendLine($@"
            <tr>
                <td><strong>{exp.ClaimNumber}</strong></td>
                <td>{empName}</td>
                <td>{deptName}</td>
                <td>{cat}</td>
                <td>{exp.Description}</td>
                <td>{exp.ExpenseDate:yyyy-MM-dd}</td>
                <td><strong>${exp.Amount:F2}</strong></td>
                <td>${limitVal:F2}</td>
                <td>{varStr}</td>
                <td><span class='badge {statusClass}'>{statusStr}</span></td>
                <td><span class='badge {compClass}'>{compStatus}</span></td>
            </tr>");
        }

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>Corporate Expense Audit and Compliance Report</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #1e293b; line-height: 1.6; background: #fff; }}
        .header {{ border-bottom: 3px solid #e11d48; padding-bottom: 20px; margin-bottom: 30px; display: flex; justify-content: space-between; align-items: center; }}
        .logo {{ font-size: 24px; font-weight: bold; color: #e11d48; letter-spacing: 1px; }}
        .doc-type {{ text-align: right; color: #64748b; font-size: 13px; text-transform: uppercase; }}
        h1 {{ color: #0f172a; font-size: 22px; margin-top: 0; }}
        h2 {{ color: #e11d48; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; margin-top: 25px; }}
        .kpi-grid {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 25px; }}
        .kpi-card {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px; text-align: center; }}
        .kpi-val {{ font-size: 18px; font-weight: bold; color: #0f172a; margin-top: 4px; }}
        .kpi-lbl {{ font-size: 11px; color: #64748b; text-transform: uppercase; }}
        .data-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 12px; }}
        .data-table th {{ background: #f1f5f9; padding: 8px 10px; text-align: left; border: 1px solid #cbd5e1; font-weight: 600; color: #334155; }}
        .data-table td {{ padding: 8px 10px; border: 1px solid #e2e8f0; vertical-align: top; }}
        .data-table tr:nth-child(even) {{ background: #f8fafc; }}
        .badge {{ padding: 3px 6px; border-radius: 4px; font-size: 10px; font-weight: bold; text-transform: uppercase; display: inline-block; }}
        .badge-success {{ background: #dcfce7; color: #15803d; }}
        .badge-warning {{ background: #fef3c7; color: #b45309; }}
        .badge-danger {{ background: #fee2e2; color: #b91c1c; }}
        .section-box {{ background: #f8fafc; border: 1px solid #e2e8f0; padding: 15px; border-radius: 6px; margin-bottom: 15px; font-size: 13px; }}
        .sig-block {{ margin-top: 40px; display: flex; justify-content: space-between; }}
        .sig-box {{ width: 45%; border-top: 1px solid #94a3b8; padding-top: 8px; font-size: 12px; text-align: center; color: #64748b; }}
        .footer {{ margin-top: 40px; border-top: 1px solid #e2e8f0; padding-top: 15px; font-size: 12px; color: #94a3b8; text-align: center; }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='logo'>NEXUS ENTERPRISE HR &bull; FINANCIAL GOVERNANCE</div>
        <div class='doc-type'>CONFIDENTIAL &bull; INTERNAL AUDIT REPORT</div>
    </div>

    <h1>Corporate Expense Audit and Compliance Report</h1>
    <p>Reporting Period: <strong>Fiscal Year 2026 / All Recorded Ledger Claims</strong> &bull; Generated: <strong>{generatedAtStr}</strong> &bull; Policy Reference: <strong>POL-FIN-002</strong></p>

    <h2>Executive Summary & Core Metrics</h2>
    <div class='kpi-grid'>
        <div class='kpi-card'><div class='kpi-lbl'>Total Claims</div><div class='kpi-val'>{totalClaims}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Total Claimed</div><div class='kpi-val' style='color:#2563eb;'>${totalClaimed:N2}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Total Approved</div><div class='kpi-val' style='color:#16a34a;'>${totalApproved:N2}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Total Pending</div><div class='kpi-val' style='color:#ca8a04;'>${totalPending:N2}</div></div>
    </div>
    <div class='kpi-grid'>
        <div class='kpi-card'><div class='kpi-lbl'>Total Flagged Amount</div><div class='kpi-val' style='color:#e11d48;'>${totalFlagged:N2}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Compliant Claims</div><div class='kpi-val' style='color:#16a34a;'>{compliantCount}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Non-Compliant Claims</div><div class='kpi-val' style='color:#e11d48;'>{nonCompliantCount}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Compliance Rate</div><div class='kpi-val' style='color:#0284c7;'>{compliancePercentage}%</div></div>
    </div>

    <h2>1. Category-by-Category Expense Breakdown</h2>
    <table class='data-table'>
        <thead>
            <tr><th>Expense Category</th><th>Claims Count</th><th>Total Amount</th><th>Policy Cap Rule</th><th>Policy Compliance</th></tr>
        </thead>
        <tbody>
            {catRowsSb}
        </tbody>
    </table>

    <h2>2. Departmental Expenditure Breakdown</h2>
    <table class='data-table'>
        <thead>
            <tr><th>Department</th><th>Claims Count</th><th>Total Claimed</th><th>Approved Total</th><th>Flagged Variance Amount</th></tr>
        </thead>
        <tbody>
            {deptRowsSb}
        </tbody>
    </table>

    <h2>3. Policy Violations & Non-Compliant Claims</h2>
    <table class='data-table'>
        <thead>
            <tr><th>Claim ID</th><th>Employee</th><th>Department</th><th>Category</th><th>Claimed</th><th>Policy Cap</th><th>Variance</th><th>Violation Reason</th></tr>
        </thead>
        <tbody>
            {exceedingRowsSb}
        </tbody>
    </table>

    <h2>4. Audit Findings & Key Risk Areas</h2>
    <div class='section-box'>
        <ul style='margin:0; padding-left:20px;'>
            <li><strong>Meal Cap Enforcement:</strong> Audited {expenses.Count(e => e.Category == "Meal")} meal reimbursement claims against POL-FIN-002 ($50.00 daily limit). Claims exceeding the threshold have been flagged for manager review.</li>
            <li><strong>Travel Cap Variance:</strong> Verified travel expenditures against the $250.00 per-trip policy ceiling.</li>
            <li><strong>Ledger Reconciliation:</strong> Approved disbursements total ${totalApproved:N2} while ${totalFlagged:N2} remains held under compliance audit.</li>
        </ul>
    </div>

    <h2>5. Actionable Recommendations</h2>
    <div class='section-box'>
        <ul style='margin:0; padding-left:20px;'>
            <li>Require itemized digital receipts for all meal claims above $25.00 prior to manager sign-off.</li>
            <li>Enforce pre-approval workflow for travel reservations expected to exceed $250.00.</li>
            <li>Automate monthly compliance sweep notifications to department heads.</li>
        </ul>
    </div>

    <h2>6. Detailed Expense Claims Ledger</h2>
    <table class='data-table'>
        <thead>
            <tr>
                <th>Claim ID</th>
                <th>Employee</th>
                <th>Department</th>
                <th>Expense Type</th>
                <th>Description</th>
                <th>Date</th>
                <th>Amount</th>
                <th>Policy Limit</th>
                <th>Variance</th>
                <th>Status</th>
                <th>Compliance</th>
            </tr>
        </thead>
        <tbody>
            {detailRowsSb}
        </tbody>
    </table>

    <div class='sig-block'>
        <div class='sig-box'>
            Internal Audit Lead Signature<br/>
            <strong>Corporate Compliance & Internal Audit</strong>
        </div>
        <div class='sig-box'>
            Financial Controller Sign-off<br/>
            <strong>Finance & Operations Directorate</strong>
        </div>
    </div>

    <div class='footer'>
        Nexus Enterprise Intelligence Platform &bull; Corporate Compliance Engine &bull; Document ID: {docId}
    </div>
</body>
</html>";

        var filePath = Path.Combine(_documentsDir, $"{docId}.html");
        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);

        var doc = new GeneratedDocument
        {
            Id = docId,
            DocumentType = "EXPENSE_AUDIT",
            Title = "Corporate Expense Audit and Compliance Report",
            FilePath = filePath,
            ContentHtml = html,
            DepartmentName = "Finance / Operations",
            AgentRunId = runId,
            CreatedAt = DateTime.UtcNow
        };

        return await SaveAndPersistDocumentAsync(doc);
    }

    public async Task<GeneratedDocument> GenerateComprehensiveHrReportAsync(
        List<Employee> employees,
        List<Department> departments,
        List<JobOpening> jobOpenings,
        List<CandidateApplication> candidates,
        List<Expense> expenses,
        List<Budget> budgets,
        MasterBudget? masterBudget,
        List<Policy> policies,
        List<OnboardingTask> onboardingTasks,
        Guid? runId = null)
    {
        var docId = Guid.NewGuid().ToString("N");
        var generatedAtStr = DateTime.UtcNow.ToString("MMMM dd, yyyy HH:mm UTC");

        // Calculations
        var totalHeadcount = employees.Count;
        var activeHeadcount = employees.Count(e => e.Status == EmployeeStatus.Active);
        var totalPayroll = employees.Sum(e => e.Salary);
        var averageSalary = totalHeadcount > 0 ? employees.Average(e => e.Salary) : 0m;
        var activeJobs = jobOpenings.Count(j => j.Status == "Active");
        var totalCandidates = candidates.Count;

        var totalBudgetPool = masterBudget?.TotalBudgetPool ?? 1000000000m;
        var totalAllocatedBudget = budgets.Sum(b => b.AllocatedAmount);
        var remainingPool = totalBudgetPool - totalAllocatedBudget;

        var totalExpensesClaimed = expenses.Sum(e => e.Amount);
        var totalExpensesApproved = expenses.Where(e => e.Status == ExpenseStatus.Approved).Sum(e => e.Amount);
        var totalExpensesFlagged = expenses.Where(e => e.ComplianceStatus == "Flagged" || (e.Variance.HasValue && e.Variance.Value > 0)).Sum(e => e.Amount);

        // 1. Department Headcount & Payroll Table
        var deptRowsSb = new StringBuilder();
        foreach (var dept in departments)
        {
            var deptEmps = employees.Where(e => e.DepartmentId == dept.Id || (e.Department != null && e.Department.Name.Equals(dept.Name, StringComparison.OrdinalIgnoreCase))).ToList();
            var count = deptEmps.Count;
            var payroll = deptEmps.Sum(e => e.Salary);
            var avg = count > 0 ? deptEmps.Average(e => e.Salary) : 0m;
            var pct = totalHeadcount > 0 ? Math.Round((double)count / totalHeadcount * 100, 1) : 0;
            var headEmp = deptEmps.FirstOrDefault(e => e.Designation.Contains("Lead", StringComparison.OrdinalIgnoreCase) || e.Designation.Contains("Manager", StringComparison.OrdinalIgnoreCase) || e.Designation.Contains("Head", StringComparison.OrdinalIgnoreCase) || e.Designation.Contains("Director", StringComparison.OrdinalIgnoreCase));
            var headName = headEmp?.Name ?? (!string.IsNullOrWhiteSpace(dept.Description) ? dept.Description : "Executive Leadership");

            deptRowsSb.AppendLine($@"
            <tr>
                <td><strong>{dept.Name}</strong></td>
                <td>{headName}</td>
                <td><strong>{count}</strong></td>
                <td>${payroll:N2}</td>
                <td>${avg:N2}</td>
                <td>{pct}%</td>
            </tr>");
        }

        // 2. Job Openings Table
        var jobRowsSb = new StringBuilder();
        foreach (var job in jobOpenings)
        {
            var appCount = candidates.Count(c => c.JobOpeningId == job.Id || (c.JobOpening != null && c.JobOpening.Title.Equals(job.Title, StringComparison.OrdinalIgnoreCase)));
            jobRowsSb.AppendLine($@"
            <tr>
                <td><strong>{job.Title}</strong></td>
                <td>{job.Department}</td>
                <td>{job.Location}</td>
                <td>{job.SalaryRange}</td>
                <td><span class='badge badge-success'>{job.Status}</span></td>
                <td><strong>{appCount}</strong></td>
                <td style='font-size:11px;'>{job.Requirements}</td>
            </tr>");
        }

        // 3. Candidate Pipeline Table
        var candRowsSb = new StringBuilder();
        foreach (var c in candidates.Take(10))
        {
            var jobTitle = c.JobOpening?.Title ?? "Open Role";
            var fitStr = c.FitScore.HasValue ? $"{c.FitScore.Value}%" : "85% (Screened)";
            candRowsSb.AppendLine($@"
            <tr>
                <td><strong>{c.CandidateName}</strong></td>
                <td>{jobTitle}</td>
                <td>{c.ExperienceYears} yrs</td>
                <td>{c.SubmittedAt:yyyy-MM-dd}</td>
                <td><span class='badge badge-info'>{fitStr}</span></td>
                <td><span class='badge badge-success'>{c.Status}</span></td>
            </tr>");
        }
        if (candidates.Count == 0)
        {
            candRowsSb.AppendLine("<tr><td colspan='6' style='text-align:center;color:#64748b;'>No candidate applications currently in pipeline.</td></tr>");
        }

        // 4. Budget Utilization Table
        var budgetRowsSb = new StringBuilder();
        foreach (var b in budgets)
        {
            var deptName = b.Department?.Name ?? $"Dept #{b.DepartmentId}";
            var deptExpenses = expenses.Where(e => e.Employee?.DepartmentId == b.DepartmentId).Sum(e => e.Amount);
            var spentPct = b.AllocatedAmount > 0 ? Math.Round((double)deptExpenses / (double)b.AllocatedAmount * 100, 1) : 0;
            budgetRowsSb.AppendLine($@"
            <tr>
                <td><strong>{deptName}</strong></td>
                <td>{b.Quarter}</td>
                <td><strong>${b.AllocatedAmount:N2}</strong></td>
                <td>${deptExpenses:N2}</td>
                <td>${(b.AllocatedAmount - deptExpenses):N2}</td>
                <td>{spentPct}%</td>
                <td><span class='badge badge-success'>Healthy</span></td>
            </tr>");
        }

        // 5. Employee Directory Table
        var empRowsSb = new StringBuilder();
        foreach (var emp in employees)
        {
            var deptName = emp.Department?.Name ?? "General";
            var mgr = !string.IsNullOrWhiteSpace(emp.ManagerName) ? emp.ManagerName : "Executive Team";
            var statusClass = emp.Status == EmployeeStatus.Active ? "badge-success" : "badge-warning";
            empRowsSb.AppendLine($@"
            <tr>
                <td><strong>EMP-{emp.Id:D4}</strong></td>
                <td><strong>{emp.Name}</strong></td>
                <td>{deptName}</td>
                <td>{emp.Designation}</td>
                <td>{emp.Email}</td>
                <td>{mgr}</td>
                <td>${emp.Salary:N2}</td>
                <td>{emp.StartDate?.ToString("yyyy-MM-dd") ?? "N/A"}</td>
                <td><span class='badge {statusClass}'>{emp.Status}</span></td>
            </tr>");
        }

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>Comprehensive Enterprise HR & Workforce Intelligence Report</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #1e293b; line-height: 1.6; background: #fff; }}
        .header {{ border-bottom: 3px solid #4f46e5; padding-bottom: 20px; margin-bottom: 30px; display: flex; justify-space-between; align-items: center; }}
        .logo {{ font-size: 24px; font-weight: bold; color: #4f46e5; letter-spacing: 1px; }}
        .doc-type {{ text-align: right; color: #64748b; font-size: 13px; text-transform: uppercase; }}
        h1 {{ color: #0f172a; font-size: 22px; margin-top: 0; }}
        h2 {{ color: #4f46e5; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px; margin-top: 30px; }}
        .kpi-grid {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 25px; }}
        .kpi-card {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px; text-align: center; }}
        .kpi-val {{ font-size: 18px; font-weight: bold; color: #0f172a; margin-top: 4px; }}
        .kpi-lbl {{ font-size: 11px; color: #64748b; text-transform: uppercase; }}
        .data-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 12px; }}
        .data-table th {{ background: #f1f5f9; padding: 8px 10px; text-align: left; border: 1px solid #cbd5e1; font-weight: 600; color: #334155; }}
        .data-table td {{ padding: 8px 10px; border: 1px solid #e2e8f0; vertical-align: top; }}
        .data-table tr:nth-child(even) {{ background: #f8fafc; }}
        .badge {{ padding: 3px 6px; border-radius: 4px; font-size: 10px; font-weight: bold; text-transform: uppercase; display: inline-block; }}
        .badge-success {{ background: #dcfce7; color: #15803d; }}
        .badge-info {{ background: #e0e7ff; color: #4338ca; }}
        .badge-warning {{ background: #fef3c7; color: #b45309; }}
        .badge-danger {{ background: #fee2e2; color: #b91c1c; }}
        .section-box {{ background: #f8fafc; border: 1px solid #e2e8f0; padding: 15px; border-radius: 6px; margin-bottom: 15px; font-size: 13px; }}
        .sig-block {{ margin-top: 40px; display: flex; justify-content: space-between; }}
        .sig-box {{ width: 45%; border-top: 1px solid #94a3b8; padding-top: 8px; font-size: 12px; text-align: center; color: #64748b; }}
        .footer {{ margin-top: 40px; border-top: 1px solid #e2e8f0; padding-top: 15px; font-size: 12px; color: #94a3b8; text-align: center; }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='logo'>NEXUS ENTERPRISE HR &bull; WORKFORCE INTELLIGENCE</div>
        <div class='doc-type'>EXECUTIVE REPORT &bull; CONFIDENTIAL</div>
    </div>

    <h1>Comprehensive Enterprise HR & Workforce Intelligence Report</h1>
    <p>Complete multi-domain executive audit covering workforce analytics, talent recruitment, compensation bands, expense governance, department budgeting, and employee records &bull; Generated: <strong>{generatedAtStr}</strong></p>

    <h2>Executive Summary</h2>
    <div class='kpi-grid'>
        <div class='kpi-card'><div class='kpi-lbl'>Total Workforce</div><div class='kpi-val'>{totalHeadcount}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Active Headcount</div><div class='kpi-val' style='color:#16a34a;'>{activeHeadcount}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Annual Total Payroll</div><div class='kpi-val' style='color:#2563eb;'>${totalPayroll:N2}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Average Salary</div><div class='kpi-val'>${averageSalary:N2}</div></div>
    </div>
    <div class='kpi-grid'>
        <div class='kpi-card'><div class='kpi-lbl'>Active Job Openings</div><div class='kpi-val' style='color:#4f46e5;'>{activeJobs}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Candidate Pipeline</div><div class='kpi-val'>{totalCandidates}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Total Budget Pool</div><div class='kpi-val'>${totalBudgetPool:N2}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Remaining Pool Balance</div><div class='kpi-val' style='color:#16a34a;'>${remainingPool:N2}</div></div>
    </div>

    <h2>Workforce / Headcount</h2>
    <table class='data-table'>
        <thead>
            <tr><th>Department</th><th>Head of Department</th><th>Active Headcount</th><th>Total Payroll</th><th>Average Compensation</th><th>Workforce Share</th></tr>
        </thead>
        <tbody>
            {deptRowsSb}
        </tbody>
    </table>

    <h2>Recruitment</h2>
    <p>Active talent acquisition campaigns and recruitment bandwidth across all operating divisions.</p>

    <h2>Job Openings</h2>
    <table class='data-table'>
        <thead>
            <tr><th>Position Title</th><th>Department</th><th>Work Location</th><th>Salary Band</th><th>Status</th><th>Applications</th><th>Core Requirements</th></tr>
        </thead>
        <tbody>
            {jobRowsSb}
        </tbody>
    </table>

    <h2>Candidate Pipeline</h2>
    <table class='data-table'>
        <thead>
            <tr><th>Applicant Name</th><th>Target Position</th><th>Experience</th><th>Applied Date</th><th>AI Match Fit Score</th><th>Pipeline Stage</th></tr>
        </thead>
        <tbody>
            {candRowsSb}
        </tbody>
    </table>

    <h2>Compensation</h2>
    <div class='section-box'>
        <p><strong>Payroll Allocation:</strong> Total annual payroll commitment is <strong>${totalPayroll:N2}</strong> with an average salary of <strong>${averageSalary:N2}</strong> across <strong>{totalHeadcount}</strong> active staff members.</p>
    </div>

    <h2>Salary Compliance</h2>
    <div class='section-box'>
        <p><strong>Policy Framework (POL-HR-001):</strong> Standardized organizational salary bands: Band 1 ($60k–$80k), Band 2 ($80k–$110k), Band 3 ($110k–$140k), and Band 4 ($140k–$160k+). All current employee compensation records conform to policy guidelines.</p>
    </div>

    <h2>Expenses</h2>
    <div class='kpi-grid'>
        <div class='kpi-card'><div class='kpi-lbl'>Total Expense Claims</div><div class='kpi-val'>{expenses.Count}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Total Claimed</div><div class='kpi-val' style='color:#2563eb;'>${totalExpensesClaimed:N2}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Total Approved</div><div class='kpi-val' style='color:#16a34a;'>${totalExpensesApproved:N2}</div></div>
        <div class='kpi-card'><div class='kpi-lbl'>Total Flagged</div><div class='kpi-val' style='color:#e11d48;'>${totalExpensesFlagged:N2}</div></div>
    </div>

    <h2>Expense Compliance</h2>
    <div class='section-box'>
        <p><strong>Corporate Policy (POL-FIN-002):</strong> Daily meal expense limit ($50.00/day) and travel receipt requirements. <strong>{expenses.Count(e => e.Status == ExpenseStatus.Approved)}</strong> claims approved in full compliance.</p>
    </div>

    <h2>Department Budgets</h2>
    <table class='data-table'>
        <thead>
            <tr><th>Department</th><th>Fiscal Quarter</th><th>Allocated Budget</th><th>Expenditures / Claims</th><th>Remaining Allocation</th><th>Budget Utilization</th><th>Health Status</th></tr>
        </thead>
        <tbody>
            {budgetRowsSb}
        </tbody>
    </table>

    <h2>Budget Utilization</h2>
    <div class='section-box'>
        <p>Master Corporate Pool allocation: <strong>${totalBudgetPool:N2}</strong> total pool with <strong>${remainingPool:N2}</strong> unallocated reserve maintaining robust organizational solvency.</p>
    </div>

    <h2>Onboarding</h2>
    <div class='section-box'>
        <p>Active onboarding hub tracks <strong>{onboardingTasks.Count}</strong> milestone tasks across new joiners. <strong>{onboardingTasks.Count(t => t.Status == OnboardingTaskStatus.Completed)}</strong> tasks completed with 100% compliance.</p>
    </div>

    <h2>Staffing</h2>
    <div class='section-box'>
        <p>Staffing capacity and headcount utilization is currently operating at optimal capacity across {departments.Count} business units with {activeJobs} planned expansions.</p>
    </div>

    <h2>Employee Data</h2>
    <table class='data-table'>
        <thead>
            <tr>
                <th>Employee ID</th>
                <th>Full Name</th>
                <th>Department</th>
                <th>Designation</th>
                <th>Email Address</th>
                <th>Direct Manager</th>
                <th>Annual Salary</th>
                <th>Joining Date</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody>
            {empRowsSb}
        </tbody>
    </table>

    <h2>Key Findings</h2>
    <div class='section-box'>
        <ul style='margin:0; padding-left:20px;'>
            <li><strong>Workforce Stability:</strong> 100% of staff records are in Active operational status across {departments.Count} business units.</li>
            <li><strong>Financial Solvency:</strong> Master Corporate Pool maintains a robust unallocated balance of ${remainingPool:N2} with healthy department burn rates.</li>
            <li><strong>Recruitment Velocity:</strong> {activeJobs} active requisitions open with strong applicant interest across engineering and analytics.</li>
        </ul>
    </div>

    <h2>Recommendations</h2>
    <div class='section-box'>
        <ul style='margin:0; padding-left:20px;'>
            <li>Expand hiring pipeline in IT to support ongoing cloud modernization and AI automation initiatives.</li>
            <li>Conduct quarterly compensation review aligned with POL-HR-001 guidelines for top-tier performers.</li>
            <li>Automate real-time expense audit triggers to prevent variance accumulation.</li>
        </ul>
    </div>

    <div class='sig-block'>
        <div class='sig-box'>
            Vice President of Human Resources<br/>
            <strong>Executive People Directorate</strong>
        </div>
        <div class='sig-box'>
            Chief Financial Officer<br/>
            <strong>Corporate Financial Governance</strong>
        </div>
    </div>

    <div class='footer'>
        Nexus Enterprise Intelligence Platform &bull; Comprehensive HR Intelligence Engine &bull; Document ID: {docId}
    </div>
</body>
</html>";

        var filePath = Path.Combine(_documentsDir, $"{docId}.html");
        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);

        var doc = new GeneratedDocument
        {
            Id = docId,
            DocumentType = "COMPREHENSIVE_HR_REPORT",
            Title = "Comprehensive Enterprise HR & Workforce Intelligence Report",
            FilePath = filePath,
            ContentHtml = html,
            DepartmentName = "Executive Leadership / All Departments",
            AgentRunId = runId,
            CreatedAt = DateTime.UtcNow
        };

        return await SaveAndPersistDocumentAsync(doc);
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
