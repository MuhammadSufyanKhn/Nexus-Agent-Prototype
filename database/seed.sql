-- =================================================================
-- NEXUS-AGENT LITE — Database Seed Data (DML Script)
-- =================================================================

USE [NexusAgentDb];
GO

-- Disable FK Constraints during seed
EXEC sp_MSforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT ALL";
GO

-- 1. Seed Users
SET IDENTITY_INSERT [dbo].[Users] ON;
MERGE INTO [dbo].[Users] AS Target
USING (VALUES
    (1, N'System Admin', N'admin@nexus.local', N'Admin', '2026-01-01T00:00:00Z'),
    (2, N'HR Manager', N'hr.manager@nexus.local', N'Manager', '2026-01-01T00:00:00Z')
) AS Source ([Id], [Name], [Email], [Role], [CreatedAt])
ON Target.[Id] = Source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Name], [Email], [Role], [CreatedAt])
    VALUES (Source.[Id], Source.[Name], Source.[Email], Source.[Role], Source.[CreatedAt]);
SET IDENTITY_INSERT [dbo].[Users] OFF;
GO

-- 2. Seed Departments
SET IDENTITY_INSERT [dbo].[Departments] ON;
MERGE INTO [dbo].[Departments] AS Target
USING (VALUES
    (1, N'IT', N'Information Technology and Systems Software Development'),
    (2, N'HR', N'Human Resources & Talent Management'),
    (3, N'Marketing', N'Brand Strategy, Growth & Digital Marketing'),
    (4, N'Operations', N'Business Operations, Logistics & Maintenance')
) AS Source ([Id], [Name], [Description])
ON Target.[Id] = Source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Name], [Description])
    VALUES (Source.[Id], Source.[Name], Source.[Description]);
SET IDENTITY_INSERT [dbo].[Departments] OFF;
GO

-- 3. Seed Q3 Budgets (IT Department Exceeds Budget: $50k Allocated vs $58.5k Spent)
SET IDENTITY_INSERT [dbo].[Budgets] ON;
MERGE INTO [dbo].[Budgets] AS Target
USING (VALUES
    (1, 1, 2026, N'Q3', 50000.00, 58500.00), -- IT OVER BUDGET (Demo 2 Target)
    (2, 2, 2026, N'Q3', 30000.00, 22000.00), -- HR Within Budget
    (3, 3, 2026, N'Q3', 45000.00, 41200.00), -- Marketing Within Budget
    (4, 4, 2026, N'Q3', 60000.00, 51000.00)  -- Operations Within Budget
) AS Source ([Id], [DepartmentId], [Year], [Quarter], [AllocatedAmount], [SpentAmount])
ON Target.[Id] = Source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [DepartmentId], [Year], [Quarter], [AllocatedAmount], [SpentAmount])
    VALUES (Source.[Id], Source.[DepartmentId], Source.[Year], Source.[Quarter], Source.[AllocatedAmount], Source.[SpentAmount]);
SET IDENTITY_INSERT [dbo].[Budgets] OFF;
GO

-- 4. Seed Employees
SET IDENTITY_INSERT [dbo].[Employees] ON;
MERGE INTO [dbo].[Employees] AS Target
USING (VALUES
    (1, N'Tariq Mahmood', N'tariq.mahmood@nexus.local', 1, N'Senior .NET Developer', 75000.00, 5, 1, '2025-03-15T00:00:00Z', '2026-01-01T00:00:00Z'),
    (2, N'Sarah Jenkins', N'sarah.jenkins@nexus.local', 1, N'Lead IT Architect', 95000.00, 8, 1, '2024-06-01T00:00:00Z', '2026-01-01T00:00:00Z'),
    (3, N'Maria Garcia', N'maria.garcia@nexus.local', 2, N'HR Specialist', 65000.00, 4, 1, '2025-01-10T00:00:00Z', '2026-01-01T00:00:00Z'),
    (4, N'Bilal Ahmed', N'bilal.ahmed@nexus.local', 4, N'Operations Lead', 70000.00, 6, 1, '2024-11-20T00:00:00Z', '2026-01-01T00:00:00Z')
) AS Source ([Id], [Name], [Email], [DepartmentId], [Designation], [Salary], [ExperienceYears], [Status], [CreatedAt], [UpdatedAt])
ON Target.[Id] = Source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Name], [Email], [DepartmentId], [Designation], [Salary], [ExperienceYears], [Status], [CreatedAt], [UpdatedAt])
    VALUES (Source.[Id], Source.[Name], Source.[Email], Source.[DepartmentId], Source.[Designation], Source.[Salary], Source.[ExperienceYears], Source.[Status], Source.[CreatedAt], Source.[UpdatedAt]);
SET IDENTITY_INSERT [dbo].[Employees] OFF;
GO

-- 5. Seed Expenses
SET IDENTITY_INSERT [dbo].[Expenses] ON;
MERGE INTO [dbo].[Expenses] AS Target
USING (VALUES
    (1, 1, 4, 150.00, '2026-07-10T00:00:00Z', 4, N'Visual Studio Pro Subscription'),
    (2, 1, 2, 350.00, '2026-08-05T00:00:00Z', 5, N'Team Lunch Expense (Exceeds $50 per person limit)'), -- NonCompliant for Demo 3
    (3, 3, 5, 400.00, '2026-07-22T00:00:00Z', 4, N'HR Compliance Certification Course')
) AS Source ([Id], [EmployeeId], [ExpenseType], [Amount], [ExpenseDate], [Status], [Description])
ON Target.[Id] = Source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [EmployeeId], [ExpenseType], [Amount], [ExpenseDate], [Status], [Description])
    VALUES (Source.[Id], Source.[EmployeeId], Source.[ExpenseType], Source.[Amount], Source.[ExpenseDate], Source.[Status], Source.[Description]);
SET IDENTITY_INSERT [dbo].[Expenses] OFF;
GO

-- 6. Seed Policies
SET IDENTITY_INSERT [dbo].[Policies] ON;
MERGE INTO [dbo].[Policies] AS Target
USING (VALUES
    (1, N'Employee Onboarding & Compensation Policy', N'HR', N'policies/HR_Policy.pdf', N'Covers onboarding requirements, standard salary bands (Mid-Level Developer $65k-$72k), equipment requests, and welcome procedures.', 1, '2026-01-01T00:00:00Z'),
    (2, N'IT System Access & Provisioning Policy', N'IT', N'policies/IT_Policy.pdf', N'Rules for developer web portal ticketing, LDAP/Active Directory provisioning, and security credentials.', 1, '2026-01-01T00:00:00Z'),
    (3, N'Expense Reimbursement & Meal Policy', N'Expense', N'policies/Expense_Policy.pdf', N'Meal expenses are capped at $50 per person. Software tools require prior IT approval. Receipts mandatory above $25.', 1, '2026-01-01T00:00:00Z'),
    (4, N'Salary Adjustment & Compensation Band Policy', N'Compensation', N'policies/Salary_Policy.pdf', N'Bulk salary increases above 5% require management approval and a formal Plan of Action impact assessment.', 1, '2026-01-01T00:00:00Z')
) AS Source ([Id], [Title], [Category], [DocumentPath], [ContentSummary], [IsActive], [UpdatedAt])
ON Target.[Id] = Source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Title], [Category], [DocumentPath], [ContentSummary], [IsActive], [UpdatedAt])
    VALUES (Source.[Id], Source.[Title], Source.[Category], Source.[DocumentPath], Source.[ContentSummary], Source.[IsActive], Source.[UpdatedAt]);
SET IDENTITY_INSERT [dbo].[Policies] OFF;
GO

-- Re-enable FK Constraints
EXEC sp_MSforeachtable "ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL";
GO
