using Microsoft.EntityFrameworkCore;
using Nexus.Agent.Intent;
using Nexus.Data.LLM;
using Nexus.Agent.Orchestration;
using Nexus.Data.Policies;
using Nexus.Data;
using Nexus.Data.Services;
using Nexus.Security.Sql;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;

var builder = WebApplication.CreateBuilder(args);

// Add Database Context with SQL Server probe & In-Memory fallback
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=NexusAgentDb;Trusted_Connection=True;TrustServerCertificate=True;";

bool isSqlServerAvailable = false;
try
{
    using var testConn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
    testConn.Open();
    isSqlServerAvailable = true;
}
catch
{
    isSqlServerAvailable = false;
}

if (isSqlServerAvailable)
{
    builder.Services.AddDbContext<NexusDbContext>(options =>
        options.UseSqlServer(connectionString, b => b.MigrationsAssembly("Nexus.Data")));
}
else
{
    builder.Services.AddDbContext<NexusDbContext>(options =>
        options.UseInMemoryDatabase("NexusAgentDb"));
}

// Register Security & Policy Engines
builder.Services.AddSingleton<ISqlValidator, SqlValidator>();
builder.Services.AddSingleton<Nexus.Security.Risk.IRiskEngine, Nexus.Security.Risk.RiskEngine>();
builder.Services.AddSingleton<Nexus.Security.Permissions.IPermissionService, Nexus.Security.Permissions.PermissionService>();
builder.Services.AddScoped<IPolicyRuleEngine, PolicyRuleEngine>();
builder.Services.AddScoped<IPolicyComplianceEngine, PolicyComplianceEngine>();

// Register Domain Application Services (DI Container)
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IPolicyService, PolicyService>();

// Register Local Open-Source LLM Subsystem
builder.Services.Configure<LLMOptions>(builder.Configuration.GetSection(LLMOptions.SectionName));

// Startup validation — catch the known bad model name early
var llmModel = builder.Configuration.GetSection(LLMOptions.SectionName).GetValue<string>("Model") ?? string.Empty;

// Provider-aware LLM service registration:
//   Provider = "Gemini"  → GeminiLLMService  (Google Generative Language API)
//   Provider = "Ollama"  → LocalLLMService   (local Ollama runtime)
var llmProvider = builder.Configuration
    .GetSection(LLMOptions.SectionName)
    .GetValue<string>("Provider") ?? "Gemini";

if (llmProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<ILLMService, GeminiLLMService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(60);
    });
    builder.Logging.AddFilter("System.Net.Http.HttpClient.GeminiLLMService", LogLevel.Warning);
}
else
{
    builder.Services.AddHttpClient<ILLMService, LocalLLMService>();
}
builder.Services.AddScoped<IIntentParser, IntentParser>();

// Register Document Generation Subsystem
builder.Services.AddScoped<IDocumentService, PdfDocumentService>();

// Register Python Automation Subsystem
builder.Services.AddSingleton<Nexus.Tools.Automation.IPythonAutomationService, Nexus.Tools.Automation.PythonAutomationService>();

// Register SAP Subsystem (Mock SAP Connector)
builder.Services.AddScoped<Nexus.Tools.Sap.ISapConnector, Nexus.Tools.Sap.MockSapConnector>();

// Register Gemini Function Calling Service
builder.Services.AddHttpClient<Nexus.Agent.LLM.GeminiFunctionCallingService>();

// Register Tool Subsystem
builder.Services.AddScoped<EmployeeCreateTool>();
builder.Services.AddScoped<EmployeeReadTool>();
builder.Services.AddScoped<EmployeeUpdateTool>();
builder.Services.AddScoped<EmployeeDeleteTool>();
builder.Services.AddScoped<DepartmentReadTool>();
builder.Services.AddScoped<BudgetReadTool>();
builder.Services.AddScoped<ExpenseReadTool>();
builder.Services.AddScoped<SqlAnalyticsTool>();
builder.Services.AddScoped<PolicyEvaluationTool>();
builder.Services.AddScoped<ComplianceTool>();
builder.Services.AddScoped<WelcomeEmailTool>();
builder.Services.AddScoped<CreateTicketTool>();
builder.Services.AddScoped<MockSapTool>();

// CRUD Tools
builder.Services.AddScoped<PolicyCrudTool>();
builder.Services.AddScoped<DepartmentCrudTool>();
builder.Services.AddScoped<BudgetUpdateTool>();
builder.Services.AddScoped<CvAnalysisTool>();
// Enterprise Workflow Tools
builder.Services.AddScoped<EmployeeTransferTool>();
builder.Services.AddScoped<EmployeeOffboardTool>();
builder.Services.AddScoped<LeaveTool>();
builder.Services.AddScoped<SlackNotifyTool>();
builder.Services.AddScoped<BudgetReallocateTool>();
builder.Services.AddScoped<BudgetFreezeTool>();
builder.Services.AddScoped<PayrollActionTool>();
builder.Services.AddScoped<BulkEmployeeUpdateTool>();
// Document & Onboarding/Offboarding/Orientation Tools
builder.Services.AddScoped<OnboardingPrepareTool>();
builder.Services.AddScoped<OffboardingInitiateTool>();
builder.Services.AddScoped<OrientationScheduleTool>();

builder.Services.AddScoped<IToolRegistry>(sp =>
{
    var registry = new ToolRegistry();
    registry.RegisterTool(sp.GetRequiredService<EmployeeCreateTool>());
    registry.RegisterTool(sp.GetRequiredService<EmployeeReadTool>());
    registry.RegisterTool(sp.GetRequiredService<EmployeeUpdateTool>());
    registry.RegisterTool(sp.GetRequiredService<EmployeeDeleteTool>());
    registry.RegisterTool(sp.GetRequiredService<DepartmentReadTool>());
    registry.RegisterTool(sp.GetRequiredService<BudgetReadTool>());
    registry.RegisterTool(sp.GetRequiredService<ExpenseReadTool>());
    registry.RegisterTool(sp.GetRequiredService<SqlAnalyticsTool>());
    registry.RegisterTool(sp.GetRequiredService<PolicyEvaluationTool>());
    registry.RegisterTool(sp.GetRequiredService<ComplianceTool>());
    registry.RegisterTool(sp.GetRequiredService<WelcomeEmailTool>());
    registry.RegisterTool(sp.GetRequiredService<CreateTicketTool>());
    registry.RegisterTool(sp.GetRequiredService<MockSapTool>());

    registry.RegisterTool(sp.GetRequiredService<PolicyCrudTool>());
    registry.RegisterTool(sp.GetRequiredService<DepartmentCrudTool>());
    registry.RegisterTool(sp.GetRequiredService<BudgetUpdateTool>());
    registry.RegisterTool(sp.GetRequiredService<CvAnalysisTool>());
    // Enterprise Workflow Tools
    registry.RegisterTool(sp.GetRequiredService<EmployeeTransferTool>());
    registry.RegisterTool(sp.GetRequiredService<EmployeeOffboardTool>());
    registry.RegisterTool(sp.GetRequiredService<LeaveTool>());
    registry.RegisterTool(sp.GetRequiredService<SlackNotifyTool>());
    registry.RegisterTool(sp.GetRequiredService<BudgetReallocateTool>());
    registry.RegisterTool(sp.GetRequiredService<BudgetFreezeTool>());
    registry.RegisterTool(sp.GetRequiredService<PayrollActionTool>());
    registry.RegisterTool(sp.GetRequiredService<BulkEmployeeUpdateTool>());
    // Document & Onboarding/Offboarding/Orientation Tools
    registry.RegisterTool(sp.GetRequiredService<OnboardingPrepareTool>());
    registry.RegisterTool(sp.GetRequiredService<OffboardingInitiateTool>());
    registry.RegisterTool(sp.GetRequiredService<OrientationScheduleTool>());
    return registry;
});

// Register Real-Time Activity Event Broadcaster & SignalR
builder.Services.AddSingleton<Nexus.Agent.Events.IAgentEventBroadcaster, Nexus.Agent.Events.AgentEventBroadcaster>();
builder.Services.AddSignalR();

// Register Agent Orchestrator
builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

// Configure CORS for local development UI & Vite Proxy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddOpenApi();

var app = builder.Build();

// Automatically ensure database schema & seed data exist on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        db.Database.EnsureCreated();

        if (db.Database.IsSqlServer())
        {
            try
            {
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'Location')
                    ALTER TABLE Employees ADD Location NVARCHAR(255) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'ManagerName')
                    ALTER TABLE Employees ADD ManagerName NVARCHAR(255) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'StartDate')
                    ALTER TABLE Employees ADD StartDate DATETIME2 NULL;

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'DepartmentId' AND is_nullable = 0)
                    ALTER TABLE Employees ALTER COLUMN DepartmentId INT NULL;

                IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Departments')
                BEGIN
                    DECLARE @defItId INT = (SELECT TOP 1 Id FROM Departments WHERE Name = 'IT');
                    IF @defItId IS NOT NULL
                        UPDATE Employees SET DepartmentId = @defItId WHERE DepartmentId IN (SELECT Id FROM Departments WHERE Name IN ('Q3', '1000000', 'Named', 'ALL'));
                    DELETE FROM Budgets WHERE DepartmentId IN (SELECT Id FROM Departments WHERE Name IN ('Q3', '1000000', 'Named', 'ALL'));
                    DELETE FROM Departments WHERE Name IN ('Q3', '1000000', 'Named', 'ALL');
                END;
            ");
        } catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Budgets') AND name = 'IsFrozen')
                    ALTER TABLE Budgets ADD IsFrozen BIT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Budgets') AND name = 'FreezeReason')
                    ALTER TABLE Budgets ADD FreezeReason NVARCHAR(500) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Budgets') AND name = 'FrozenAt')
                    ALTER TABLE Budgets ADD FrozenAt DATETIME2 NULL;
            ");
        } catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Leaves')
                BEGIN
                    CREATE TABLE Leaves (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(Id) ON DELETE CASCADE,
                        LeaveType INT NOT NULL,
                        StartDate DATETIME2 NOT NULL,
                        EndDate DATETIME2 NOT NULL,
                        Status INT NOT NULL DEFAULT 1,
                        Reason NVARCHAR(500) NULL,
                        Notes NVARCHAR(500) NULL,
                        ApprovedBy NVARCHAR(255) NULL,
                        AgentRunId UNIQUEIDENTIFIER NULL,
                        ProcessedAt DATETIME2 NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;
            ");
        } catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Leaves')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Leaves') AND name = 'AgentRunId')
                        ALTER TABLE Leaves ADD AgentRunId UNIQUEIDENTIFIER NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Leaves') AND name = 'ProcessedAt')
                        ALTER TABLE Leaves ADD ProcessedAt DATETIME2 NULL;
                END;
            ");
        } catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Budgets')
                BEGIN
                    DECLARE @itDeptId INT = (SELECT TOP 1 Id FROM Departments WHERE Name = 'IT');
                    IF @itDeptId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Budgets WHERE DepartmentId = @itDeptId)
                        INSERT INTO Budgets (DepartmentId, Year, Quarter, AllocatedAmount, SpentAmount, IsFrozen)
                        VALUES (@itDeptId, 2026, 'Q3', 50000.00, 58500.00, 0);
                END;
            ");
        } catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tickets')
                BEGIN
                    CREATE TABLE Tickets (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        TicketId NVARCHAR(50) NOT NULL,
                        EmployeeName NVARCHAR(150) NOT NULL,
                        Department NVARCHAR(100) NOT NULL,
                        RequestType NVARCHAR(150) NOT NULL,
                        Priority NVARCHAR(50) NOT NULL DEFAULT 'High',
                        Status NVARCHAR(50) NOT NULL DEFAULT 'Open',
                        Details NVARCHAR(MAX) NOT NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;
            ");
        } catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MasterBudgets')
                BEGIN
                    CREATE TABLE MasterBudgets (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Year INT NOT NULL DEFAULT 2026,
                        FiscalYear NVARCHAR(50) NOT NULL DEFAULT '2026-2027',
                        TotalBudgetPool DECIMAL(18,2) NOT NULL DEFAULT 1000000000.00,
                        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                    INSERT INTO MasterBudgets (Year, FiscalYear, TotalBudgetPool, UpdatedAt)
                    VALUES (2026, '2026-2027', 1000000000.00, GETUTCDATE());
                END;
            ");
        } catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JobOpenings')
                BEGIN
                    CREATE TABLE JobOpenings (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Title NVARCHAR(150) NOT NULL,
                        Department NVARCHAR(100) NOT NULL,
                        Description NVARCHAR(MAX) NOT NULL,
                        Requirements NVARCHAR(2000) NOT NULL,
                        Location NVARCHAR(100) NOT NULL DEFAULT 'Remote / Hybrid',
                        SalaryRange NVARCHAR(100) NOT NULL DEFAULT '$75,000 - $95,000',
                        Status NVARCHAR(50) NOT NULL DEFAULT 'Active',
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                    INSERT INTO JobOpenings (Title, Department, Description, Requirements, Location, SalaryRange, Status, CreatedAt)
                    VALUES 
                    ('Senior Full Stack Developer', 'IT', 'Seeking an experienced Senior Full Stack Developer to lead design and development of our enterprise AI and workforce automation platforms.', 'C#, .NET Core 8.0, ASP.NET Core, React.js, TypeScript, SQL Server 2022, Entity Framework Core, RESTful APIs, Microservices, Docker', 'Remote / Hybrid', '$80,000 - $105,000', 'Active', GETUTCDATE()),
                    ('Web Developer', 'IT', 'Looking for a talented Web Developer to build high-performance, accessible, and responsive user interfaces for workforce management solutions.', 'React, TypeScript, JavaScript, HTML5, CSS3, REST APIs, Tailwind CSS, Component Design Systems, State Management', 'Remote / Hybrid', '$65,000 - $85,000', 'Active', GETUTCDATE());
                END;

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CandidateApplications')
                BEGIN
                    CREATE TABLE CandidateApplications (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        JobOpeningId INT NOT NULL FOREIGN KEY REFERENCES JobOpenings(Id) ON DELETE CASCADE,
                        CandidateName NVARCHAR(150) NOT NULL,
                        Email NVARCHAR(150) NOT NULL,
                        Phone NVARCHAR(50) NOT NULL,
                        ExperienceYears INT NOT NULL DEFAULT 3,
                        CoverNote NVARCHAR(MAX) NOT NULL,
                        CvText NVARCHAR(MAX) NOT NULL,
                        CvFileName NVARCHAR(250) NOT NULL,
                        Status NVARCHAR(50) NOT NULL DEFAULT 'Submitted',
                        FitScore INT NULL,
                        AiEvaluationJson NVARCHAR(MAX) NULL,
                        SubmittedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                    INSERT INTO CandidateApplications (JobOpeningId, CandidateName, Email, Phone, ExperienceYears, CoverNote, CvText, CvFileName, Status, FitScore, SubmittedAt)
                    VALUES 
                    (1, 'Ali Khan', 'ali.khan@nexus.local', '+92-300-1234567', 4, 'Passionate Full Stack engineer with 4+ years building high-throughput .NET Core APIs and responsive React applications.', 'CANDIDATE RESUME: Ali Khan\r\nEmail: ali.khan@nexus.local | Phone: +92-300-1234567 | Location: Lahore, PK\r\n\r\nSUMMARY:\r\nResults-driven Software Engineer with 4+ years of hands-on experience building enterprise Web APIs, Microservices, and SQL Server databases using C#, .NET Core, ASP.NET, Entity Framework, and React.js.\r\n\r\nTECHNICAL SKILLS:\r\n- Languages: C#, JavaScript, TypeScript, SQL\r\n- Frameworks: .NET Core 8.0, ASP.NET Core, Entity Framework Core, React, Redux\r\n- Databases: SQL Server 2022, T-SQL, Redis\r\n- Tools: Git, Docker, Azure DevOps, Postman, Visual Studio 2022', 'Ali_Khan_Resume.pdf', 'Submitted', 88, GETUTCDATE());
                END;
                ELSE
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CandidateApplications') AND name = 'CvPdfData')
                    BEGIN
                        ALTER TABLE CandidateApplications ADD CvPdfData NVARCHAR(MAX) NULL;
                    END;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('JobOpenings') AND name = 'Responsibilities')
                BEGIN
                    ALTER TABLE JobOpenings ADD Responsibilities NVARCHAR(MAX) NULL;
                END;
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('JobOpenings') AND name = 'Responsibilities')
                BEGIN
                    UPDATE JobOpenings SET Responsibilities = '' WHERE Responsibilities IS NULL;
                END;

                IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Policies')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM Policies WHERE Code = 'POL-HR-003')
                    BEGIN
                        INSERT INTO Policies (Code, Title, Category, DocumentPath, ContentSummary, IsActive, UpdatedAt)
                        VALUES ('POL-HR-003', 'Remote Work & Hybrid Attendance Policy', 'HR', 'policies/Remote_Work_Policy.pdf', 'Eligible employees may work remotely up to 2 days per week. Core collaboration hours are 10:00 AM to 4:00 PM. A one-time home office equipment stipend of up to $500 is provided with manager approval.', 1, GETUTCDATE());
                    END;
                END;
            ");
        } catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                UPDATE Employees SET Email = '4t195es@gmail.com' WHERE Name LIKE '%Sufyan%' OR Name LIKE '%sufyan%';
                UPDATE Employees SET Email = 'sufyankhankhattak33@gmail.com' WHERE Name LIKE '%Ali%' OR Name LIKE '%ali%';
                UPDATE Employees SET Email = REPLACE(Email, '@nexus.local', '@gmail.com') WHERE Email LIKE '%@nexus.local';
            ");
        } catch { }
        }

        // Auto-reseed defaults on startup removed so deleted entities stay deleted across app restarts.
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database initial connection warning: Schema sync skipped or failed.");
    }
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "NEXUS-AGENT LITE API",
    status = "Online",
    version = "1.0.0",
    openApi = "/openapi/v1.json",
    health = "/api/agent/health"
}));
app.MapHub<Nexus.Api.Hubs.AgentActivityHub>("/hubs/agent-activity");

app.Run();
