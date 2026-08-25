# NEXUS-AGENT LITE — System Architecture Specification

This document provides a technical specification of the architecture, security model, and execution lifecycle of **NEXUS-AGENT LITE**.

---

## 1. High-Level Architecture Diagram

```
                               ┌──────────────────────────────────────────────┐
                               │        React 19 Enterprise UI               │
                               │   (Dashboard, Agent Console, Approvals)     │
                               └──────────────────────┬───────────────────────┘
                                                      │ HTTP / SignalR WebSockets
                                                      ▼
                               ┌──────────────────────────────────────────────┐
                               │        ASP.NET Core 10 Web API               │
                               │     (AgentController, ApprovalController)    │
                               └──────────────────────┬───────────────────────┘
                                                      │
                                                      ▼
                               ┌──────────────────────────────────────────────┐
                               │             AgentOrchestrator                │
                               └──────┬───────────────┬───────────────┬───────┘
                                      │               │               │
            ┌─────────────────────────┘               │               └─────────────────────────┐
            ▼                                         ▼                                         ▼
┌───────────────────────┐                 ┌───────────────────────┐                 ┌───────────────────────┐
│     Intent Parser     │                 │   Security Sandbox    │                 │   Policy & Compliance │
│ (Local LLM / Fallback)│                 │ (RiskEngine / RBAC)   │                 │ (Rule Engine / Audit) │
└───────────────────────┘                 └───────────┬───────────┘                 └───────────────────────┘
                                                      │
                                                      ▼
                                          ┌───────────────────────┐
                                          │ ActionPlan Interceptor│
                                          │ (Human-in-the-Loop)   │
                                          └───────────┬───────────┘
                                                      │ (Approved)
                                                      ▼
                                          ┌───────────────────────┐
                                          │     ToolRegistry      │
                                          └───────────┬───────────┘
                                                      │
         ┌──────────────────────┬─────────────────────┼─────────────────────┬──────────────────────┐
         ▼                      ▼                     ▼                     ▼                      ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  Employee Tools  │  │  SQL Analytics   │  │ Python Automation│  │    Playwright    │  │ Mock SAP ERP HCM │
│   (SQL Server)   │  │   (Sandbox)      │  │  (Email / Ticket)│  │ (Legacy Portal)  │  │   (Connector)    │
└──────────────────┘  └──────────────────┘  └──────────────────┘  └──────────────────┘  └──────────────────┘
                                                      │
                                                      ▼
                                          ┌───────────────────────┐
                                          │ SHA-256 Audit Ledger  │
                                          └───────────────────────┘
```

---

## 2. Core Subsystems

### 2.1 Intent Parsing Engine (`Nexus.Agent/Intent/`)
- **Primary Parser**: `IntentParser` invokes `ILLMService.GenerateJsonAsync<ParsedIntentResult>()` against local Ollama inference server (`llama3.2`).
- **Structured JSON Schema**: Returns `intent`, `entities` (name, department, designation, salary), `confidence`, and `requiresClarification`.
- **Deterministic Rule Fallback**: If LLM output is malformed or offline, `RuleBasedFallbackParse` handles intent extraction without breaking pipeline continuity.

### 2.2 Security Sandbox & Permission Service (`Nexus.Security/`)
- **RBAC Matrix**: Enforces permissions (`employee.create`, `employee.read`, `employee.update`, `employee.delete`, `sql.analytics`).
- **Risk Classification**:
  - `LOW`: Automatic execution permitted (`employee.read`, `sql.analytics`, `email.welcome`).
  - `MEDIUM`: Controlled execution (`employee.create`, `onboarding.submit_legacy_form`, `sap.employee.create`).
  - `HIGH`: Requires Plan of Action human confirmation (`employee.update`, `EMPLOYEE_ONBOARDING`).
  - `CRITICAL`: Requires administrative authorization (`employee.delete`).

### 2.3 Human-in-the-Loop Plan of Action Interceptor (`Nexus.Data/ActionPlan/`)
- **Pre-Execution Calculation**: Computes affected record count, old vs. new values diffs, total financial impact (`+$23,800.00`), and safety warnings.
- **State Lock**: Sets `AgentRunStatus.WaitingForApproval` with **ZERO database mutations** occurring before human decision.
- **Approval API**: `POST /api/approval/decide` approves (`approved: true`) or rejects (`approved: false`).

### 2.4 SQL Security Sandbox (`Nexus.Security/Sql/` & `Nexus.Tools/Implementations/SqlAnalyticsTool.cs`)
- **Read-Only Mode**: Accepts ONLY `SELECT` analytics queries.
- **Forbidden Keyword Filter**: Rejects `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `EXEC`, `xp_cmdshell`.
- **Resource Constraints**: Enforces `TOP 100` row cap and 5s query execution timeout.

### 2.5 Multi-System Automation Subsystems
- **Legacy HR Portal Automation** (`mock-portal/` & `automation/browser/legacy_hr_portal.py`): Playwright script navigating, filling form fields `#employeeName`, `#department`, `#designation`, `#salary`, `#manager`, and submitting form.
- **Mock SAP Connector** (`Nexus.Tools/Sap/`): `ISapConnector` interface generating simulated SAP Personnel Numbers (`SAP-EMP-2026-XXXX`).
- **Python Email & Ticket Generators** (`automation/email_services/` & `automation/tickets/`): CLI runner generating IT onboarding welcome emails.

### 2.6 Cryptographic Audit Ledger (`Nexus.Data/Entities/AuditLog.cs`)
- **Tamper-Proof SHA-256 Hash Chain**: Every tool execution appends a cryptographic hash derived from `runId`, `userId`, `toolName`, `action`, `result`, `timestamp`, and `previousHash`.

---

## 3. Technology Stack Summary

- **Backend**: C# .NET 10 Web API, Entity Framework Core 10, ASP.NET Core SignalR.
- **Frontend**: React 19, TypeScript 5.6, Tailwind CSS v4, Lucide Icons.
- **Automation**: Python 3.12, Playwright Chromium Headless.
- **Local AI**: Ollama Local Inference Engine (`llama3.2`).
