# NEXUS-AGENT LITE
> **FROM INTENT TO ACTION — SECURELY.**

[![Framework](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Frontend](https://img.shields.io/badge/React-19-blue.svg)](https://react.dev/)
[![Styling](https://img.shields.io/badge/Tailwind_CSS-v4-06B6D4.svg)](https://tailwindcss.com/)
[![Python](https://img.shields.io/badge/Python-3.12-yellow.svg)](https://www.python.org/)
[![Playwright](https://img.shields.io/badge/Playwright-Browser_Automation-green.svg)](https://playwright.dev/)
[![LLM](https://img.shields.io/badge/LLM-Local_Ollama_Llama3.2-orange.svg)](https://ollama.com/)

**NEXUS-AGENT LITE** is an autonomous enterprise AI execution engine built on .NET 10, React 19, and Python Playwright. It converts natural language business requests into multi-system operational workflows across SQL Server databases, legacy enterprise web portals, SAP ERP HCM, and automated IT ticketing systems—enforcing strict **Backend Supremacy**, **Role-Based Access Control (RBAC)**, **Natural Language SQL Security Sandboxing**, **Human-in-the-Loop Pre-Execution Plan of Action Gates**, and **Tamper-Proof SHA-256 Cryptographic Audit Logs**.

---

## Key Enterprise Security & Control Features

- 🔒 **Zero Cloud Dependency & Local AI Privacy**: Uses a local open-source LLM (Ollama / Llama3.2) for intent parsing without sending enterprise data to paid cloud APIs.
- 🛡️ **Backend Supremacy & Non-Arbitrary Tool Execution**: The LLM cannot directly execute code, SQL, shell commands, or browser JavaScript. All operations are mediated by strongly-typed registered `.NET` tools.
- 🛑 **Human-in-the-Loop Plan of Action Interceptor**: High-risk or multi-system actions (`EMPLOYEE_ONBOARDING`, `EMPLOYEE_UPDATE`, `EMPLOYEE_DELETE`) calculate an impact preview (affected records, old vs. new values diffs, financial impact) and pause execution with **ZERO database mutations** until human confirmation.
- 🔍 **Natural Language to SQL Security Sandbox**: Strict SQL Validator permits ONLY read-only `SELECT` queries, rejecting destructive commands (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `EXEC`, `xp_cmdshell`), capping `TOP 100` rows, and enforcing 5s execution timeouts.
- 🌐 **Multi-System Automation**: Integrates SQL Server master databases, Legacy HR Portal Playwright form entry (`mock-portal/`), Mock SAP ERP HCM master data provisioning, and Python Welcome Email generation.
- 📜 **Tamper-Proof SHA-256 Cryptographic Audit Chain**: Every tool execution appends a cryptographically linked SHA-256 hash entry to an immutable audit ledger.

---

## 🚀 Quick Start & Environment Execution

### System Requirements
- **.NET 10 SDK** (v10.0+)
- **Node.js** (v20+) & **npm**
- **Python 3.12** (`py` launcher installed at `C:\Windows\py.exe` or `python`)
- **SQL Server / LocalDB** or In-Memory fallback
- **Ollama** (Running locally at `http://localhost:11434` with model `llama3.2`)

---

### Step 1: Start Mock Legacy HR Portal
```bash
py mock-portal/server.py
# Running at http://127.0.0.1:8088/
```

### Step 2: Start ASP.NET Core Web API Backend
```bash
cd backend/Nexus.Api
dotnet run --launch-profile https
# Running at https://localhost:7160 / http://localhost:5160
```

### Step 3: Start React 19 Command Center Frontend
```bash
cd frontend
npm run dev
# Command Center UI running at http://localhost:3000
```

---

## 🎬 5 Hackathon Demo Scenarios

Launch the React Command Center at `http://localhost:3000` and use the quick prompt badges in the **Agent Console**:

| Demo Scenario | Quick Prompt | Primary System Output & Verification |
|---|---|---|
| **Demo 1: AI Onboarding Agent** | `"Onboard Ahmed Khan as a mid-level .NET developer in IT according to company policy. Apply the correct salary, create his employee record, submit the legacy IT onboarding form, create his Mock SAP record, and generate his welcome email."` | 1. Evaluates policy `POL-HR-001` ($68,000.00 salary).<br>2. Displays Plan of Action (`AWAITING_APPROVAL`).<br>3. Proves ZERO database mutation before human approval.<br>4. Upon human confirmation, creates SQL record, submits Playwright form (`HR-REC-2026-8812`), provisions Mock SAP (`SAP-EMP-2026-8912`), and generates welcome email. |
| **Demo 2: AI SQL Analyst** | `"Show me departments exceeding their allocated Q3 budget."` | 1. Converts natural language to T-SQL query.<br>2. Validates query against SQL Security Sandbox.<br>3. Returns formatted data table with column insights & business summary. |
| **Demo 3: Policy Compliance** | `"Check whether Ahmed's latest expense complies with company policy."` | 1. Retrieves employee claim & policy `POL-FIN-002`.<br>2. Calculates financial limit variance ($85.00 claimed vs $50.00 limit = $35.00 overflow).<br>3. Displays compliance evidence citation. |
| **Demo 4: Human Approval Gate** | `"Increase salaries of all IT developers by 10%."` | 1. Calculates change impact preview ($75,000 → $82,500, +$23,800.00 total impact).<br>2. Displays Plan of Action (`AWAITING_APPROVAL`).<br>3. Zero database changes occur until human operator clicks **Confirm Action**. |
| **Demo 5: Security / Critical Gate** | `"Delete all employees."` | 1. Intent classified as `EMPLOYEE_DELETE` (Critical Risk).<br>2. Execution intercepted by Critical Security Gate; unauthorized roles blocked with 403 Security Violation. |

---

## 📑 Documentation Index

- 📘 [**SETUP_GUIDE.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/SETUP_GUIDE.md): Complete installation, database seeding, Python Playwright setup, and local LLM configuration instructions.
- 🎬 [**DEMO_GUIDE.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/DEMO_GUIDE.md): Step-by-step hackathon presentation guide walking through Demos 1 through 5.
- 🏗️ [**ARCHITECTURE.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/ARCHITECTURE.md): System architecture specification, security model, risk engine matrix, and tool registry reference.

---

## 🧪 Running Automated Unit & Integration Tests

```bash
cd backend
dotnet test Nexus.Tests/Nexus.Tests.csproj
```
**Test Results:** `Passed! - Failed: 0, Passed: 71, Skipped: 0, Total: 71` across all 71 unit, integration, RBAC, SQL sandbox, failure handling, and end-to-end demo tests.
