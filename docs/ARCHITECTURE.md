# System Architecture & Technical Design

This document explains how **NEXUS-AGENT LITE** is built, how data flows between the frontend, backend, AI parser, and database, and how security controls are enforced.

---

## 1. High-Level System Flow Diagram

```
┌────────────────────────────────────────────────────────────────────────┐
│                        React 19 Enterprise UI                          │
│               (Dashboard, Agent Console, Approval Center)              │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ HTTP REST & SignalR WebSockets
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        ASP.NET Core 10 Web API                         │
│               (AgentController, ApprovalController, LeaveController)   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                          AgentOrchestrator                             │
└───────────────┬───────────────────┼───────────────────┬────────────────┘
                │                   │                   │
                ▼                   ▼                   ▼
     ┌────────────────────┐ ┌───────────────────┐ ┌───────────────────┐
     │ Intent Parsing     │ │ Security Sandbox  │ │ Human Approval    │
     │ Engine (Gemini/LLM)│ │ (RBAC & Risk)     │ │ Interceptor       │
     └────────────────────┘ └─────────┬─────────┘ └─────────┬─────────┘
                                      │                     │
                                      ▼                     ▼
                            ┌───────────────────────────────────┐
                            │      Registered Tool Registry     │
                            └─────────────────┬─────────────────┘
                                              │
        ┌───────────────────┬─────────────────┼─────────────────┬───────────────────┐
        ▼                   ▼                 ▼                 ▼                   ▼
┌───────────────┐   ┌───────────────┐ ┌───────────────┐ ┌───────────────┐   ┌───────────────┐
│ SQL Server DB │   │  SQL Sandbox  │ │ Python Email  │ │  Playwright   │   │ Mock SAP HCM  │
│ (EF Core 10)  │   │  (Analytics)  │ │ (SMTP Runner) │ │ (Mock Portal) │   │ (Connector)   │
└───────────────┘   └───────────────┘ └───────────────┘ └───────────────┘   └───────────────┘
                                              │
                                              ▼
                                    ┌───────────────────┐
                                    │ SHA-256 Audit Log │
                                    └───────────────────┘
```

---

## 2. Core Subsystems Explained

### 2.1 Intent Parsing Engine (`Nexus.Agent/Intent/`)
* **What it does**: Translates human text prompts into structured JSON actions.
* **Dual Parsing Engine**: Uses Gemini API / local LLM service for AI understanding with a deterministic C# regex fallback (`RuleBasedFallbackParse`) so intent parsing never fails even if offline.
* **Extracted Attributes**: Generates intent type (`EMPLOYEE_ONBOARDING`, `LEAVE_CREATE`, `BUDGET_UPDATE`), parameters (name, department, role, salary, date), and confidence score.

---

### 2.2 Security Sandbox & Permission Service (`Nexus.Security/`)
* **Role-Based Access Control (RBAC)**: Checks user credentials before running actions.
* **Risk Engine Matrix**:
  * `LOW` (Auto-execute): Read queries, leave balance lookup, email generation.
  * `MEDIUM` (Controlled): Standard employee creation, legacy portal form submission.
  * `HIGH` (Requires Approval): Salary adjustments, budget allocations, department transfers.
  * `CRITICAL` (Requires Admin Gate): Employee deletion, system-wide purges.

---

### 2.3 Human-in-the-Loop Plan of Action Interceptor (`Nexus.Data/ActionPlan/`)
* **Zero Database Mutations Until Approved**: For high-risk plans, the system generates a detailed **Action Plan** (listing affected records, previous vs. proposed values, total financial impact) and pauses in state `WaitingForApproval`.
* **Interactive Approval UI**: Admins can inspect the proposed diff table in the Approval Center and click **Authorize & Execute Plan** or **Reject Action**.

---

### 2.4 SQL Security Sandbox (`Nexus.Security/Sql/` & `SqlAnalyticsTool.cs`)
* **Read-Only Data Analytics**: Allows users to ask questions about workforce data in plain English.
* **Security Rules**: Rejects any destructive statements (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `EXEC`), enforces `TOP 100` row limits, and applies a 5-second execution timeout.

---

### 2.5 Multi-System Automation Engine (`automation/`)
* **Python SMTP Subsystem**: Dispatches HTML emails via `runner.py` directly to target email addresses (e.g. `nexusagent.notifications@gmail.com` and employee DB emails).
* **Playwright Browser Automation**: Fills forms in the legacy HR portal (`mock-portal/`) headlessly.
* **Mock SAP Connector**: Generates simulated SAP Personnel Numbers (`SAP-EMP-2026-XXXX`).

---

### 2.6 SHA-256 Cryptographic Audit Ledger (`Nexus.Data/Entities/AuditLog.cs`)
* **Immutable Audit Trail**: Every tool execution appends a cryptographic hash chain calculated from `runId`, `userId`, `toolName`, `action`, `result`, and `previousHash` to guarantee compliance.

---

## 3. Technology Stack Overview

| Layer | Technologies Used |
| :--- | :--- |
| **Backend API** | C# .NET 10 Web API, EF Core 10, ASP.NET Core SignalR WebSockets |
| **Frontend** | React 19, TypeScript 5.6, Tailwind CSS v4, Lucide Icons |
| **Automation** | Python 3.12, Playwright Chromium Headless, SMTP Mailer |
| **AI / Intent Parsing** | Gemini API / Local LLM Engine + C# Rule-based Fallback |
| **Database** | SQL Server (LocalDB / Express / Developer Edition) |
