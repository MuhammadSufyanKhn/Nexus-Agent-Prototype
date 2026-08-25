-- ===============================================================================
-- NEXUS-AGENT ENTERPRISE DATABASE SCHEMA DDL SCRIPT FOR SQL SERVER
-- Database: NexusAgentDb
-- Target Engine: Microsoft SQL Server 2019+ / SQL Server Management Studio (SSMS)
-- ===============================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'NexusAgentDb')
BEGIN
    CREATE DATABASE [NexusAgentDb];
END
GO

USE [NexusAgentDb];
GO

-- -------------------------------------------------------------------------------
-- 1. DEPARTMENTS MASTER TABLE
-- -------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Departments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Departments] (
        [Id]          INT IDENTITY(1,1) NOT NULL,
        [Name]        NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_Departments_Name] UNIQUE ([Name])
    );
END
GO

-- -------------------------------------------------------------------------------
-- 2. EMPLOYEES MASTER TABLE
-- -------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employees]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employees] (
        [Id]              INT IDENTITY(1,1) NOT NULL,
        [Name]            NVARCHAR(150) NOT NULL,
        [Email]           NVARCHAR(150) NOT NULL,
        [DepartmentId]    INT NOT NULL,
        [Designation]     NVARCHAR(100) NOT NULL,
        [Salary]          DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [ExperienceYears] INT NOT NULL DEFAULT 0,
        [Status]          INT NOT NULL DEFAULT 1, -- 1=Active, 2=Inactive, 3=Onboarding
        [CreatedAt]       DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt]       DATETIME2(7) NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Employees_Departments] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments] ([Id]) ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_Employees_DepartmentId] ON [dbo].[Employees]([DepartmentId] ASC);
    CREATE NONCLUSTERED INDEX [IX_Employees_Name] ON [dbo].[Employees]([Name] ASC);
END
GO

-- -------------------------------------------------------------------------------
-- 3. DEPARTMENT BUDGETS TABLE
-- -------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Budgets]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Budgets] (
        [Id]              INT IDENTITY(1,1) NOT NULL,
        [DepartmentId]    INT NOT NULL,
        [Quarter]         NVARCHAR(10) NOT NULL DEFAULT N'Q3',
        [Year]            INT NOT NULL DEFAULT 2026,
        [AllocatedAmount] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [SpentAmount]     DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [CreatedAt]       DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Budgets] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Budgets_Departments] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Budgets_DepartmentId_Quarter_Year] ON [dbo].[Budgets]([DepartmentId] ASC, [Quarter] ASC, [Year] ASC);
END
GO

-- -------------------------------------------------------------------------------
-- 4. HR POLICIES KNOWLEDGE TABLE
-- -------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Policies]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Policies] (
        [Id]             INT IDENTITY(1,1) NOT NULL,
        [Code]           NVARCHAR(50) NOT NULL,
        [Title]          NVARCHAR(200) NOT NULL,
        [Category]       NVARCHAR(100) NOT NULL DEFAULT N'HR',
        [ContentSummary] NVARCHAR(MAX) NOT NULL,
        [DocumentPath]   NVARCHAR(500) NULL,
        [IsActive]       BIT NOT NULL DEFAULT 1,
        [UpdatedAt]      DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Policies] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_Policies_Code] UNIQUE ([Code])
    );
END
GO

-- -------------------------------------------------------------------------------
-- 5. EXPENSE CLAIMS TABLE
-- -------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Expenses] (
        [Id]          INT IDENTITY(1,1) NOT NULL,
        [EmployeeId]  INT NOT NULL,
        [Category]    NVARCHAR(100) NOT NULL,
        [Amount]      DECIMAL(18,2) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [Status]      INT NOT NULL DEFAULT 1, -- 1=Submitted, 2=Approved, 3=Rejected
        [SubmittedAt] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Expenses] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Expenses_Employees] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE CASCADE
    );
END
GO

-- -------------------------------------------------------------------------------
-- 6. AGENT RUNS ENGINE EXECUTION TABLE
-- -------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AgentRuns]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AgentRuns] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [UserId]         INT NULL,
        [OriginalPrompt] NVARCHAR(MAX) NOT NULL,
        [Intent]         NVARCHAR(150) NOT NULL,
        [Status]         INT NOT NULL DEFAULT 0, -- 0=Pending, 1=Executing, 2=Completed, 3=Failed, 4=AwaitingApproval, 5=Rejected
        [StartedAt]      DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [CompletedAt]    DATETIME2(7) NULL,
        CONSTRAINT [PK_AgentRuns] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- -------------------------------------------------------------------------------
-- 7. APPROVALS HUMAN-IN-THE-LOOP INTERCEPTOR TABLE
-- -------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Approvals]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Approvals] (
        [Id]          UNIQUEIDENTIFIER NOT NULL,
        [AgentRunId]  UNIQUEIDENTIFIER NOT NULL,
        [ActionId]    NVARCHAR(100) NULL,
        [RequestedBy] NVARCHAR(100) NOT NULL DEFAULT N'System',
        [RiskLevel]   INT NOT NULL DEFAULT 2, -- 0=Low, 1=Medium, 2=High, 3=Critical
        [Status]      INT NOT NULL DEFAULT 1, -- 1=Pending, 2=Approved, 3=Rejected
        [Reason]      NVARCHAR(MAX) NULL,
        [ApprovedBy]  NVARCHAR(100) NULL,
        [ApprovedAt]  DATETIME2(7) NULL,
        [CreatedAt]   DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Approvals] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Approvals_AgentRuns] FOREIGN KEY ([AgentRunId]) REFERENCES [dbo].[AgentRuns] ([Id]) ON DELETE CASCADE
    );
END
GO

-- -------------------------------------------------------------------------------
-- 8. AUDIT LOGS CRYPTOGRAPHIC LEDGER TABLE
-- -------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id]           INT IDENTITY(1,1) NOT NULL,
        [AgentRunId]   UNIQUEIDENTIFIER NULL,
        [UserId]       INT NULL,
        [Action]       NVARCHAR(150) NOT NULL,
        [ToolName]     NVARCHAR(100) NOT NULL,
        [Target]       NVARCHAR(100) NOT NULL,
        [Result]       NVARCHAR(MAX) NULL,
        [Timestamp]    DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [PreviousHash] NVARCHAR(128) NOT NULL,
        [CurrentHash]  NVARCHAR(128) NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- ===============================================================================
-- INITIAL REFERENCE DATA SEED SCRIPT
-- ===============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Departments])
BEGIN
    INSERT INTO [dbo].[Departments] ([Name], [Description]) VALUES 
    (N'IT', N'Software Engineering & Infrastructure'),
    (N'HR', N'Human Resources & Talent Management'),
    (N'Finance', N'Corporate Finance & Payroll'),
    (N'Marketing', N'Product Marketing & Communications');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[Policies])
BEGIN
    INSERT INTO [dbo].[Policies] ([Code], [Title], [Category], [ContentSummary]) VALUES 
    (N'POL-HR-001', N'Annual Leave Policy', N'HR', N'Employees receive 20 annual paid leave days per year with standard manager notice.'),
    (N'POL-FIN-002', N'Expense Travel Limit', N'Finance', N'Maximum daily travel allowance is $150. Expenses exceeding $150 require executive approval.'),
    (N'POL-HR-003', N'Software Engineer Compensation Band', N'HR', N'Junior .NET Developer: $60k-$90k. Senior Developer: $100k-$180k.');
END
GO
