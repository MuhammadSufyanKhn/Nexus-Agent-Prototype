-- ===============================================================================
-- NEXUS AGENT MANUAL SQL SCHEMA UPDATE & AUDIT VERIFICATION SCRIPT
-- Database: NexusAgentDb
-- Target: Microsoft SQL Server Management Studio (SSMS)
-- Description: Bulletproof DDL script with null-checks and default constraints.
-- ===============================================================================

USE [NexusAgentDb];
GO

-- 1. If Code column exists on Departments, set a default constraint so INSERTs without Code don't fail
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Departments]') AND name = N'Code')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[Departments]') AND name = N'DF_Departments_Code')
    BEGIN
        ALTER TABLE [dbo].[Departments] ADD CONSTRAINT [DF_Departments_Code] DEFAULT N'DEPT' FOR [Code];
    END
END
GO

-- 2. Ensure Employees table has required columns
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employees]') AND name = N'ExperienceYears')
BEGIN
    ALTER TABLE [dbo].[Employees] ADD [ExperienceYears] INT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employees]') AND name = N'UpdatedAt')
BEGIN
    ALTER TABLE [dbo].[Employees] ADD [UpdatedAt] DATETIME2(7) NULL;
END
GO

-- 3. Ensure Departments table has Description column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Departments]') AND name = N'Description')
BEGIN
    ALTER TABLE [dbo].[Departments] ADD [Description] NVARCHAR(500) NULL;
END
GO

-- 4. Ensure Policies table has Code and DocumentPath columns
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Policies]') AND name = N'Code')
BEGIN
    ALTER TABLE [dbo].[Policies] ADD [Code] NVARCHAR(50) NOT NULL DEFAULT N'POL-GEN-000';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Policies]') AND name = N'DocumentPath' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[Policies] ALTER COLUMN [DocumentPath] NVARCHAR(500) NULL;
END
GO

-- 5. Safe Seed Insertion for Departments (Supplies Code if column exists in table)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Departments] WHERE [Name] = N'Finance')
BEGIN
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Departments]') AND name = N'Code')
    BEGIN
        INSERT INTO [dbo].[Departments] ([Code], [Name], [Description]) VALUES (N'FIN', N'Finance', N'Corporate Finance & Payroll');
    END
    ELSE
    BEGIN
        INSERT INTO [dbo].[Departments] ([Name], [Description]) VALUES (N'Finance', N'Corporate Finance & Payroll');
    END
END
GO

PRINT 'NexusAgentDb schema verification complete. All columns and tables up to date.';
GO
