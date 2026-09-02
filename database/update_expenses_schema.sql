USE [NexusAgentDb];
GO

-- 1. Add missing columns to dbo.Expenses
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'ClaimNumber')
    ALTER TABLE [dbo].[Expenses] ADD [ClaimNumber] NVARCHAR(50) NOT NULL CONSTRAINT DF_Expenses_ClaimNumber DEFAULT '';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'Category')
    ALTER TABLE [dbo].[Expenses] ADD [Category] NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'ComplianceStatus')
    ALTER TABLE [dbo].[Expenses] ADD [ComplianceStatus] NVARCHAR(50) NOT NULL CONSTRAINT DF_Expenses_ComplianceStatus DEFAULT 'Pending';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'PolicyLimit')
    ALTER TABLE [dbo].[Expenses] ADD [PolicyLimit] DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'Variance')
    ALTER TABLE [dbo].[Expenses] ADD [Variance] DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'FlagReason')
    ALTER TABLE [dbo].[Expenses] ADD [FlagReason] NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'ReviewedBy')
    ALTER TABLE [dbo].[Expenses] ADD [ReviewedBy] NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'ReviewedDate')
    ALTER TABLE [dbo].[Expenses] ADD [ReviewedDate] DATETIME2 NULL;
GO

-- 2. Ensure Employee Sarah Ahmed exists in dbo.Employees
IF NOT EXISTS (SELECT * FROM [dbo].[Employees] WHERE [Name] = N'Sarah Ahmed' OR [Name] LIKE N'%Sarah Ahmed%')
BEGIN
    DECLARE @DeptId INT;
    SELECT TOP 1 @DeptId = [Id] FROM [dbo].[Departments] WHERE [Name] = 'Operations' OR [Name] = 'IT';
    IF @DeptId IS NULL SET @DeptId = 1;

    INSERT INTO [dbo].[Employees] ([Name], [Email], [DepartmentId], [Designation], [Salary], [ExperienceYears], [Status], [CreatedAt], [UpdatedAt])
    VALUES (N'Sarah Ahmed', N'sarah.ahmed@nexus.local', @DeptId, N'Operations Consultant', 72000.00, 4, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
END
GO

-- 3. Seed Realistic Expense Claims if table has 0 rows or is missing EXP-4012 / EXP-3089
DECLARE @SarahId INT, @AhmedId INT, @SufyanId INT, @StaceyId INT;
SELECT TOP 1 @SarahId = [Id] FROM [dbo].[Employees] WHERE [Name] LIKE N'%Sarah Ahmed%';
SELECT TOP 1 @AhmedId = [Id] FROM [dbo].[Employees] WHERE [Name] LIKE N'%Ahmed Khan%';
SELECT TOP 1 @SufyanId = [Id] FROM [dbo].[Employees] WHERE [Name] LIKE N'%Sufyan%';
SELECT TOP 1 @StaceyId = [Id] FROM [dbo].[Employees] WHERE [Name] LIKE N'%Stacey%';

IF @SarahId IS NULL SELECT TOP 1 @SarahId = [Id] FROM [dbo].[Employees];
IF @AhmedId IS NULL SELECT TOP 1 @AhmedId = [Id] FROM [dbo].[Employees];
IF @SufyanId IS NULL SELECT TOP 1 @SufyanId = [Id] FROM [dbo].[Employees];
IF @StaceyId IS NULL SELECT TOP 1 @StaceyId = [Id] FROM [dbo].[Employees];

IF NOT EXISTS (SELECT * FROM [dbo].[Expenses] WHERE [ClaimNumber] = 'EXP-4012')
BEGIN
    INSERT INTO [dbo].[Expenses] ([ClaimNumber], [EmployeeId], [ExpenseType], [Category], [Amount], [ExpenseDate], [Status], [ComplianceStatus], [PolicyLimit], [Variance], [FlagReason], [Description])
    VALUES ('EXP-4012', @SarahId, 1, 'Travel', 220.00, '2026-09-01T10:00:00Z', 1, 'Compliant', 250.00, -30.00, NULL, 'Travel reimbursement for client site visit');
END

IF NOT EXISTS (SELECT * FROM [dbo].[Expenses] WHERE [ClaimNumber] = 'EXP-3089')
BEGIN
    INSERT INTO [dbo].[Expenses] ([ClaimNumber], [EmployeeId], [ExpenseType], [Category], [Amount], [ExpenseDate], [Status], [ComplianceStatus], [PolicyLimit], [Variance], [FlagReason], [Description])
    VALUES ('EXP-3089', @SarahId, 1, 'Travel', 320.00, '2026-09-01T11:30:00Z', 1, 'Flagged', 250.00, 70.00, 'Claim of $320.00 exceeds allowed Travel policy limit of $250.00.', 'Regional flight and ground transit');
END

IF NOT EXISTS (SELECT * FROM [dbo].[Expenses] WHERE [Category] = 'Equipment' AND [Amount] = 850.00)
BEGIN
    INSERT INTO [dbo].[Expenses] ([ClaimNumber], [EmployeeId], [ExpenseType], [Category], [Amount], [ExpenseDate], [Status], [ComplianceStatus], [PolicyLimit], [Variance], [FlagReason], [Description])
    VALUES ('EXP-5104', @AhmedId, 3, 'Equipment', 850.00, '2026-09-01T14:15:00Z', 1, 'NonCompliant', 500.00, 350.00, 'Equipment claim exceeds $500 threshold without pre-approval.', 'Ergonomic Standing Desk and Dual Arm Rig');
END

IF NOT EXISTS (SELECT * FROM [dbo].[Expenses] WHERE [ClaimNumber] = 'EXP-2041')
BEGIN
    INSERT INTO [dbo].[Expenses] ([ClaimNumber], [EmployeeId], [ExpenseType], [Category], [Amount], [ExpenseDate], [Status], [ComplianceStatus], [PolicyLimit], [Variance], [FlagReason], [Description])
    VALUES ('EXP-2041', @SufyanId, 2, 'Meal', 75.00, '2026-09-02T09:00:00Z', 1, 'Flagged', 50.00, 25.00, 'Claim of $75.00 exceeds $50.00 daily meal per diem cap.', 'Executive strategy lunch');
END

IF NOT EXISTS (SELECT * FROM [dbo].[Expenses] WHERE [ClaimNumber] = 'EXP-1018')
BEGIN
    INSERT INTO [dbo].[Expenses] ([ClaimNumber], [EmployeeId], [ExpenseType], [Category], [Amount], [ExpenseDate], [Status], [ComplianceStatus], [PolicyLimit], [Variance], [FlagReason], [Description])
    VALUES ('EXP-1018', @StaceyId, 2, 'Meal', 45.00, '2026-09-02T10:15:00Z', 2, 'Compliant', 50.00, -5.00, NULL, 'Working team breakfast');
END

IF NOT EXISTS (SELECT * FROM [dbo].[Expenses] WHERE [ClaimNumber] = 'EXP-1019')
BEGIN
    INSERT INTO [dbo].[Expenses] ([ClaimNumber], [EmployeeId], [ExpenseType], [Category], [Amount], [ExpenseDate], [Status], [ComplianceStatus], [PolicyLimit], [Variance], [FlagReason], [Description])
    VALUES ('EXP-1019', @AhmedId, 1, 'Travel', 295.00, '2026-09-02T11:00:00Z', 1, 'Flagged', 250.00, 45.00, 'Travel expenses exceed $250.00 cap by $45.00.', 'Inter-city express rail booking');
END

IF NOT EXISTS (SELECT * FROM [dbo].[Expenses] WHERE [ClaimNumber] = 'EXP-1020')
BEGIN
    INSERT INTO [dbo].[Expenses] ([ClaimNumber], [EmployeeId], [ExpenseType], [Category], [Amount], [ExpenseDate], [Status], [ComplianceStatus], [PolicyLimit], [Variance], [FlagReason], [Description])
    VALUES ('EXP-1020', @SarahId, 4, 'Software', 450.00, '2026-09-02T12:00:00Z', 2, 'Compliant', 500.00, -50.00, NULL, 'Figma Enterprise Annual License');
END
GO
