# NEXUS-AGENT LITE — Enterprise AI HR Assistant

> **Turn Natural Language Requests into Safe, Automated HR & Operations Workflows.**

NEXUS-AGENT LITE is an enterprise AI HR assistant built using **C# .NET 10 Web API**, **React 19**, and **Python 3.12 Automation**. It allows HR managers and administrators to talk to their HR systems in plain English. 

Whether you want to onboard a new employee, log a sick day, reallocate department budgets, or analyze workforce spending, Nexus AI understands your natural language commands and safely performs the operations.

---

## 🌟 Key Features Made Simple

### 1. 🤖 Intelligent Natural Language Assistant
Simply type what you want to do in plain English into the chat console:
* *"Onboard Ahmed Khan as a mid-level .NET developer in IT"*
* *"Log Sufyan's sick day today and notify his team"*
* *"Show me departments exceeding their allocated Q3 budget"*

### 2. 🛡️ Human-in-the-Loop Approval Center
For high-risk operations (like onboarding an employee, changing salaries, or transferring staff), Nexus AI creates a **Plan of Action** and pauses execution. **Zero database changes occur** until an administrator reviews the financial impact, record diffs, and clicks **Authorize & Execute Plan**.

### 3. 📧 Automated Email & Notification Subsystem
Automatically dispatches HTML notification emails via Python SMTP directly to target recipients (e.g. `nexusagent.notifications@gmail.com` and the employee's database email) without hardcoded dummy addresses.

### 4. 🔒 Built-in Security Sandbox & Role-Based Access Control (RBAC)
* **SQL Security Sandbox**: Converts natural language to T-SQL, but permits **ONLY read-only `SELECT` queries** for data analytics (rejecting any destructive commands like `DROP` or `DELETE`).
* **RBAC Engine**: Enforces role-based permissions so unauthorized users cannot run high-risk operations.

### 5. 📜 Cryptographic Audit Ledger
Every single action executed by the agent appends a tamper-proof **SHA-256 hash log** to an immutable audit ledger for complete enterprise compliance.

---

## 🚀 Easy Quick Start Guide

### Prerequisites
* **.NET 10 SDK** installed on your system
* **Node.js** (v20+) & **npm**
* **Python 3.12** (`py` launcher installed)

---

### Step 1: Start Mock Legacy HR Portal
In a terminal window:
```bash
py mock-portal/server.py
```
*(Runs local legacy HR portal at `http://127.0.0.1:8088`)*

---

### Step 2: Start ASP.NET Core Backend API
In a second terminal window:
```bash
cd backend/Nexus.Api
dotnet run
```
*(Runs backend Web API at `https://localhost:7160` / `http://localhost:5160`)*

---

### Step 3: Start React Command Center Frontend
In a third terminal window:
```bash
cd frontend
npm run dev
```
*(Open your browser at `http://localhost:3000`)*

---

## 🎬 5 Core System Demo Scenarios

Launch the web app at `http://localhost:3000` and test these example commands in the **Agent Console**:

| Demo Scenario | Example Natural Language Command | What Happens |
| :--- | :--- | :--- |
| **1. AI Employee Onboarding** | `"Onboard Ahmed Khan as a mid-level .NET developer in IT with salary 68k"` | Evaluates policy `POL-HR-001`, generates a Plan of Action, creates SQL record, fills legacy HR portal form, provisions Mock SAP, and sends welcome email upon approval. |
| **2. Sick Leave & Notification** | `"Log Sufyan's sick day today and notify his team on gmail"` | Looks up employee Sufyan in database (`4t195es@gmail.com`), logs sick leave in SQL Server, and sends notification email to both the team inbox and employee. |
| **3. SQL Data Analytics** | `"Show me departments exceeding their allocated Q3 budget"` | Converts query to secure T-SQL, validates against SQL sandbox, and displays budget spending insights. |
| **4. Human Approval Gate** | `"Increase salaries of all IT developers by 10%"` | Calculates total financial impact preview, creates a visual diff table, and pauses for human administrator authorization. |
| **5. Policy Compliance Check** | `"Check whether Ahmed's latest expense complies with company policy"` | Compares claimed expense against `POL-FIN-002` rules ($50 meal limit) and displays compliance audit evidence. |

---

## 📁 Repository Documentation Index

* 🏗️ [**docs/ARCHITECTURE.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/docs/ARCHITECTURE.md): System architecture, subsystem flow, and security model explained in plain English.
* ⚙️ [**docs/AUTOMATION.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/docs/AUTOMATION.md): Python email automation, Playwright browser automation, and ticket creation details.
* 🎬 [**docs/DEMO_GUIDE.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/docs/DEMO_GUIDE.md): Step-by-step walkthrough of all hackathon demonstration scenarios.
* 📘 [**docs/SETUP_GUIDE.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/docs/SETUP_GUIDE.md): Complete local setup and configuration instructions.
* 🤖 [**docs/AI_INFRASTRUCTURE.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/docs/AI_INFRASTRUCTURE.md): Explanation of Gemini API integration, local LLM fallback, and intent parsing.
* 📋 [**docs/POLICIES.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/docs/POLICIES.md): HR policy rules and financial limit guidelines.
* 🗄️ [**database/README.md**](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/database/README.md): Database schema, SQL tables, and seed data layout.

---

## 🧪 Running Automated Unit Tests

```bash
cd backend
dotnet test Nexus.Tests/Nexus.Tests.csproj
```
*(All unit and integration tests run cleanly with zero failures!)*
