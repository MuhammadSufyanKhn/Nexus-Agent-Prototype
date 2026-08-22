# NEXUS-AGENT LITE — System Architecture

> **Tagline:** FROM INTENT TO ACTION — SECURELY.

NEXUS-AGENT LITE is an AI-powered business-process execution agent that translates natural language requests into policy-compliant, permission-validated multi-step operations.

---

## 1. High-Level Architecture Flow

```
User Request (Natural Language)
  │
  ▼
React 19 UI (Vite + Tailwind CSS)
  │ (HTTP / WebSockets API)
  ▼
ASP.NET Core Web API (.NET 9)
  │
  ▼
Nexus Agent Engine (Nexus.Agent)
  ├─ Intent Parser (Local LLM Runtime)
  ├─ Policy Engine (PDF RAG & Rules)
  ├─ Permission & Risk Engine (Nexus.Security)
  └─ Planner (Multi-step Execution Plans & POA)
  │
  ▼
Human Approval Gate (Required for Medium/High/Critical operations)
  │
  ▼
Tool Execution Router (Nexus.Tools)
  ├─ Safe SQL Engine (EF Core / Read-Only & Entity Guards)
  ├─ Policy Retriever Tool
  ├─ Automation Engine (Python / Playwright)
  ├─ Mock SAP Connector
  └─ Notification & Email Automation
  │
  ▼
Audit & Log Layer (Nexus.Data DbContext Audit Trail)
  │
  ▼
User Activity Feed & Result Visualizer
```

---

## 2. Core Components & Responsibilities

### Backend (.NET 9 Solution)
- **`Nexus.Api`**: REST API endpoints, WebSockets streaming execution traces, authentication, and request handling.
- **`Nexus.Agent`**: Core agent logic, Intent Parser interface, Planner engine, and execution orchestrator.
- **`Nexus.Tools`**: Registered execution tools (SQL, Policy RAG, Playwright, SAP, Email).
- **`Nexus.Security`**: Permission engine, risk level evaluator, SQL AST validator, read-only enforcement, and safety guardrails.
- **`Nexus.Data`**: EF Core DbContext, SQL Server schema entities, Audit Trail models, and seeding scripts.

### Frontend (React + TypeScript + Vite)
- Interactive Chat & Intent Input bar
- Plan of Action (POA) modal with affected rows, old vs new value diffs, and risk badges
- Safe Real-time Execution Feed (filtering private Chain-of-Thought)
- Interactive human confirmation & audit viewer

### Automation (Python + Playwright)
- Playwright script handlers for legacy IT onboarding portals
- Mock SAP BAPI/OData provisioning module

---

## 3. Security Principles & Guardrails

1. **LLM Execution Restriction**: The LLM NEVER directly executes SQL, shell scripts, browser JS, or file IO. It can ONLY request actions through registered tools.
2. **Backend Authority**: Backend (.NET 9 API & `Nexus.Security`) is the single source of truth for permissions, risk, and validation.
3. **SQL Guardrails**:
   - Read-only enforcement for analytical queries (blocking `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `EXEC`, `xp_cmdshell`).
   - Allowed table/column whitelist filtering, strict query timeout, and maximum row limit caps.
4. **Plan of Action (POA)**: High-risk or bulk update operations generate a pre-execution diff payload and block until explicit human confirmation is received.
5. **Private Chain-of-Thought**: Internal reasoning remains strictly on the server; only sanitized progress steps (e.g. `1. Intent identified`, `2. Policy retrieved`, `3. Plan generated`) are emitted to the client.
