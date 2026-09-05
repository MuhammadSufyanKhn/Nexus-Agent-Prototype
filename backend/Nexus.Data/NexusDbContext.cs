using System;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.Entities;
using Nexus.Data.Enums;

namespace Nexus.Data;

public class NexusDbContext : DbContext
{
    public NexusDbContext(DbContextOptions<NexusDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<MasterBudget> MasterBudgets => Set<MasterBudget>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentAction> AgentActions => Set<AgentAction>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OnboardingTask> OnboardingTasks => Set<OnboardingTask>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<JobOpening> JobOpenings => Set<JobOpening>();
    public DbSet<CandidateApplication> CandidateApplications => Set<CandidateApplication>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Department Configuration
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Employee Configuration
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Designation).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Salary).HasPrecision(18, 2);
            entity.Property(e => e.DepartmentId).IsRequired(false);
            entity.HasOne(e => e.Department)
                  .WithMany(d => d.Employees)
                  .HasForeignKey(e => e.DepartmentId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.DepartmentId);
        });

        // Budget Configuration
        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quarter).IsRequired().HasMaxLength(10);
            entity.Property(e => e.AllocatedAmount).HasPrecision(18, 2);
            entity.Property(e => e.SpentAmount).HasPrecision(18, 2);
            entity.HasOne(b => b.Department)
                  .WithMany(d => d.Budgets)
                  .HasForeignKey(b => b.DepartmentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(b => new { b.DepartmentId, b.Year, b.Quarter });
        });

        // MasterBudget Configuration
        modelBuilder.Entity<MasterBudget>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.TotalBudgetPool).HasPrecision(18, 2);
            entity.Property(m => m.FiscalYear).HasMaxLength(50);
        });

        // Expense Configuration
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.PolicyLimit).HasPrecision(18, 2);
            entity.Property(e => e.Variance).HasPrecision(18, 2);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasOne(e => e.Employee)
                  .WithMany(emp => emp.Expenses)
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.EmployeeId);
        });

        // Policy Configuration
        modelBuilder.Entity<Policy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DocumentPath).IsRequired().HasMaxLength(300);
            entity.HasIndex(e => e.Category);
        });

        // AgentRun Configuration
        modelBuilder.Entity<AgentRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalPrompt).IsRequired();
            entity.Property(e => e.Intent).IsRequired().HasMaxLength(150);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.AgentRuns)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // AgentAction Configuration
        modelBuilder.Entity<AgentAction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ToolName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.AgentRun)
                  .WithMany(r => r.Actions)
                  .HasForeignKey(e => e.AgentRunId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.AgentRunId);
        });

        // Approval Configuration
        modelBuilder.Entity<Approval>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequestedBy).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.AgentRun)
                  .WithMany(r => r.Approvals)
                  .HasForeignKey(e => e.AgentRunId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AgentAction)
                  .WithMany(a => a.Approvals)
                  .HasForeignKey(e => e.ActionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog Configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ToolName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CurrentHash).IsRequired().HasMaxLength(128);
            entity.HasOne(e => e.AgentRun)
                  .WithMany(r => r.AuditLogs)
                  .HasForeignKey(e => e.AgentRunId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.AuditLogs)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.Timestamp);
        });

        // OnboardingTask Configuration
        modelBuilder.Entity<OnboardingTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TaskName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.SystemTarget).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Employee)
                  .WithMany(emp => emp.OnboardingTasks)
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AgentRun)
                  .WithMany(r => r.OnboardingTasks)
                  .HasForeignKey(e => e.AgentRunId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Leave Configuration
        modelBuilder.Entity<Leave>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.ApprovedBy).HasMaxLength(100);
            entity.HasOne(e => e.Employee)
                  .WithMany(emp => emp.Leaves)
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.Status);
        });

        // JobOpening Configuration
        modelBuilder.Entity<JobOpening>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Title).IsRequired().HasMaxLength(150);
            entity.Property(j => j.Department).IsRequired().HasMaxLength(100);
            entity.Property(j => j.Requirements).HasMaxLength(2000);
            entity.Property(j => j.Location).HasMaxLength(100);
            entity.Property(j => j.SalaryRange).HasMaxLength(100);
            entity.Property(j => j.Status).HasMaxLength(50);
        });

        // CandidateApplication Configuration
        modelBuilder.Entity<CandidateApplication>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CandidateName).IsRequired().HasMaxLength(150);
            entity.Property(c => c.Email).IsRequired().HasMaxLength(150);
            entity.Property(c => c.Phone).HasMaxLength(50);
            entity.Property(c => c.Status).HasMaxLength(50);
            entity.HasOne(c => c.JobOpening)
                  .WithMany(j => j.Applications)
                  .HasForeignKey(c => c.JobOpeningId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(c => c.JobOpeningId);
        });

        // Seed Initial Data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // 1. Users
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Name = "System Admin", Email = "admin@nexus.local", Role = "Admin", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new User { Id = 2, Name = "HR Manager", Email = "hr.manager@nexus.local", Role = "Manager", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // 2. Departments
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "IT", Description = "Information Technology and Systems Software Development" },
            new Department { Id = 2, Name = "HR", Description = "Human Resources & Talent Management" },
            new Department { Id = 3, Name = "Marketing", Description = "Brand Strategy, Growth & Digital Marketing" },
            new Department { Id = 4, Name = "Operations", Description = "Business Operations, Logistics & Maintenance" }
        );

        // 3. Budgets (Q3 2026) - Note: IT Allocated $50k, Spent $58.5k (EXCEEDED for Demo 2)
        modelBuilder.Entity<Budget>().HasData(
            new Budget { Id = 1, DepartmentId = 1, Year = 2026, Quarter = "Q3", AllocatedAmount = 50000.00m, SpentAmount = 58500.00m, IsFrozen = false },
            new Budget { Id = 2, DepartmentId = 2, Year = 2026, Quarter = "Q3", AllocatedAmount = 30000.00m, SpentAmount = 22000.00m, IsFrozen = false },
            new Budget { Id = 3, DepartmentId = 3, Year = 2026, Quarter = "Q3", AllocatedAmount = 45000.00m, SpentAmount = 41200.00m, IsFrozen = false },
            new Budget { Id = 4, DepartmentId = 4, Year = 2026, Quarter = "Q3", AllocatedAmount = 60000.00m, SpentAmount = 51000.00m, IsFrozen = false }
        );


        // 4. Existing Employees
        modelBuilder.Entity<Employee>().HasData(
            new Employee { Id = 1, Name = "Tariq Mahmood", Email = "tariq.mahmood@gmail.com", DepartmentId = 1, Designation = "Senior .NET Developer", Salary = 75000.00m, ExperienceYears = 5, Status = EmployeeStatus.Active, CreatedAt = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Employee { Id = 2, Name = "Sarah Jenkins", Email = "sarah.jenkins@gmail.com", DepartmentId = 1, Designation = "Lead IT Architect", Salary = 95000.00m, ExperienceYears = 8, Status = EmployeeStatus.Active, CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Employee { Id = 3, Name = "Maria Garcia", Email = "maria.garcia@gmail.com", DepartmentId = 2, Designation = "HR Specialist", Salary = 65000.00m, ExperienceYears = 4, Status = EmployeeStatus.Active, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Employee { Id = 4, Name = "Bilal Ahmed", Email = "bilal.ahmed@gmail.com", DepartmentId = 4, Designation = "Operations Lead", Salary = 70000.00m, ExperienceYears = 6, Status = EmployeeStatus.Active, CreatedAt = new DateTime(2024, 11, 20, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // 5. Expenses (Including compliant and non-compliant claims for Demo 3)
        modelBuilder.Entity<Expense>().HasData(
            new Expense { Id = 1, EmployeeId = 1, ExpenseType = ExpenseType.Software, Amount = 150.00m, ExpenseDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc), Status = ExpenseStatus.Compliant, Description = "Visual Studio Pro Subscription" },
            new Expense { Id = 2, EmployeeId = 1, ExpenseType = ExpenseType.Meal, Amount = 350.00m, ExpenseDate = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc), Status = ExpenseStatus.NonCompliant, Description = "Team Lunch Expense (Exceeds $50 per person limit)" },
            new Expense { Id = 3, EmployeeId = 3, ExpenseType = ExpenseType.Training, Amount = 400.00m, ExpenseDate = new DateTime(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc), Status = ExpenseStatus.Compliant, Description = "HR Compliance Certification Course" }
        );

        // 6. Policies
        modelBuilder.Entity<Policy>().HasData(
            new Policy { Id = 1, Code = "POL-HR-001", Title = "Employee Onboarding & Compensation Policy", Category = "HR", DocumentPath = "policies/HR_Policy.pdf", ContentSummary = "Covers onboarding requirements, standard salary bands (Mid-Level Developer $65k-$72k), equipment requests, and welcome procedures.", IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Policy { Id = 2, Code = "POL-IT-001", Title = "IT System Access & Provisioning Policy", Category = "IT", DocumentPath = "policies/IT_Policy.pdf", ContentSummary = "Rules for developer web portal ticketing, LDAP/Active Directory provisioning, and security credentials.", IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Policy { Id = 3, Code = "POL-FIN-002", Title = "Expense Reimbursement & Meal Policy", Category = "Finance", DocumentPath = "policies/Expense_Policy.pdf", ContentSummary = "Meal expenses are capped at $50 per person. Software tools require prior IT approval. Receipts mandatory above $25.", IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Policy { Id = 4, Code = "POL-HR-002", Title = "Salary Adjustment & Compensation Band Policy", Category = "HR", DocumentPath = "policies/Salary_Policy.pdf", ContentSummary = "Bulk salary increases above 5% require management approval and a formal Plan of Action impact assessment.", IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Policy { Id = 5, Code = "POL-HR-003", Title = "Remote Work & Hybrid Attendance Policy", Category = "HR", DocumentPath = "policies/Remote_Work_Policy.pdf", ContentSummary = "Eligible employees may work remotely up to 2 days per week. Core collaboration hours are 10:00 AM to 4:00 PM. A one-time home office equipment stipend of up to $500 is provided with manager approval.", IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // 7. IT Provisioning Tickets
        modelBuilder.Entity<Ticket>().HasData(
            new Ticket { Id = 1, TicketId = "TCK-2026-4829", EmployeeName = "Tariq Mahmood", Department = "IT", RequestType = "Hardware & Software Provisioning", Priority = "High", Status = "Resolved", Details = "Workstation laptop, Visual Studio Pro, VPN access provisioned.", CreatedAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc) },
            new Ticket { Id = 2, TicketId = "TCK-2026-5102", EmployeeName = "Sarah Jenkins", Department = "IT", RequestType = "Security Clearance & Admin Access", Priority = "High", Status = "Resolved", Details = "Elevated admin privileges and cloud infrastructure access granted.", CreatedAt = new DateTime(2026, 8, 10, 11, 30, 0, DateTimeKind.Utc) },
            new Ticket { Id = 3, TicketId = "TCK-2026-6941", EmployeeName = "Ahmed Khan", Department = "IT", RequestType = "Hardware Provisioning", Priority = "High", Status = "Open", Details = "MacBook Pro M3 Max 32GB, Dual 4K Monitors, YubiKey setup.", CreatedAt = new DateTime(2026, 8, 25, 09, 15, 0, DateTimeKind.Utc) }
        );

        // 8. Job Openings
        modelBuilder.Entity<JobOpening>().HasData(
            new JobOpening
            {
                Id = 1,
                Title = "Senior Full Stack Developer",
                Department = "IT",
                Description = "Seeking an experienced Senior Full Stack Developer to lead design and development of our enterprise AI and workforce automation platforms.",
                Requirements = "C#, .NET Core 8.0, ASP.NET Core, React.js, TypeScript, SQL Server 2022, Entity Framework Core, RESTful APIs, Microservices, Docker",
                Location = "Remote / Hybrid",
                SalaryRange = "$80,000 - $105,000",
                Status = "Active",
                CreatedAt = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new JobOpening
            {
                Id = 2,
                Title = "Web Developer",
                Department = "IT",
                Description = "Looking for a talented Web Developer to build high-performance, accessible, and responsive user interfaces for workforce management solutions.",
                Requirements = "React, TypeScript, JavaScript, HTML5, CSS3, REST APIs, Tailwind CSS, Component Design Systems, State Management",
                Location = "Remote / Hybrid",
                SalaryRange = "$65,000 - $85,000",
                Status = "Active",
                CreatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // 9. Initial Candidate Applications
        modelBuilder.Entity<CandidateApplication>().HasData(
            new CandidateApplication
            {
                Id = 1,
                JobOpeningId = 1,
                CandidateName = "Ali Khan",
                Email = "ali.khan@nexus.local",
                Phone = "+92-300-1234567",
                ExperienceYears = 4,
                CoverNote = "Passionate Full Stack engineer with 4+ years building high-throughput .NET Core APIs and responsive React applications.",
                CvText = @"CANDIDATE RESUME: Ali Khan
Email: ali.khan@nexus.local | Phone: +92-300-1234567 | Location: Lahore, PK

SUMMARY:
Results-driven Software Engineer with 4+ years of hands-on experience building enterprise Web APIs, Microservices, and SQL Server databases using C#, .NET Core, ASP.NET, Entity Framework, and React.js.

TECHNICAL SKILLS:
- Languages: C#, JavaScript, TypeScript, SQL
- Frameworks: .NET Core 8.0, ASP.NET Core, Entity Framework Core, React, Redux
- Databases: SQL Server 2022, T-SQL, Redis
- Tools: Git, Docker, Azure DevOps, Postman, Visual Studio 2022

EXPERIENCE:
Senior Software Developer — TechCorp Solutions (2022 - Present)
- Designed and delivered high-performance RESTful Web APIs serving 50k+ daily active users.
- Optimized EF Core queries and SQL Server indexes, reducing DB query latency by 45%.
- Implemented JWT authentication and Role-Based Access Control (RBAC) security matrix.

WORK EXPERIENCE:
Software Engineer — SoftCode Systems (2020 - 2022)
- Built responsive React dashboards integrated with C# backend services.
- Participated in CI/CD pipeline automation and unit testing using xUnit.

EDUCATION:
BS Computer Science — Fast University (2020)",
                CvFileName = "Ali_Khan_Resume.pdf",
                Status = "Submitted",
                FitScore = 88,
                SubmittedAt = new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}

