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
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentAction> AgentActions => Set<AgentAction>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OnboardingTask> OnboardingTasks => Set<OnboardingTask>();

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

        // Expense Configuration
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
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
            new Budget { Id = 1, DepartmentId = 1, Year = 2026, Quarter = "Q3", AllocatedAmount = 50000.00m, SpentAmount = 58500.00m },
            new Budget { Id = 2, DepartmentId = 2, Year = 2026, Quarter = "Q3", AllocatedAmount = 30000.00m, SpentAmount = 22000.00m },
            new Budget { Id = 3, DepartmentId = 3, Year = 2026, Quarter = "Q3", AllocatedAmount = 45000.00m, SpentAmount = 41200.00m },
            new Budget { Id = 4, DepartmentId = 4, Year = 2026, Quarter = "Q3", AllocatedAmount = 60000.00m, SpentAmount = 51000.00m }
        );

        // 4. Existing Employees
        modelBuilder.Entity<Employee>().HasData(
            new Employee { Id = 1, Name = "Tariq Mahmood", Email = "tariq.mahmood@nexus.local", DepartmentId = 1, Designation = "Senior .NET Developer", Salary = 75000.00m, ExperienceYears = 5, Status = EmployeeStatus.Active, CreatedAt = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Employee { Id = 2, Name = "Sarah Jenkins", Email = "sarah.jenkins@nexus.local", DepartmentId = 1, Designation = "Lead IT Architect", Salary = 95000.00m, ExperienceYears = 8, Status = EmployeeStatus.Active, CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Employee { Id = 3, Name = "Maria Garcia", Email = "maria.garcia@nexus.local", DepartmentId = 2, Designation = "HR Specialist", Salary = 65000.00m, ExperienceYears = 4, Status = EmployeeStatus.Active, CreatedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Employee { Id = 4, Name = "Bilal Ahmed", Email = "bilal.ahmed@nexus.local", DepartmentId = 4, Designation = "Operations Lead", Salary = 70000.00m, ExperienceYears = 6, Status = EmployeeStatus.Active, CreatedAt = new DateTime(2024, 11, 20, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
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
            new Policy { Id = 4, Code = "POL-HR-002", Title = "Salary Adjustment & Compensation Band Policy", Category = "HR", DocumentPath = "policies/Salary_Policy.pdf", ContentSummary = "Bulk salary increases above 5% require management approval and a formal Plan of Action impact assessment.", IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
