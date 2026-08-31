# NEXUS-AGENT LITE — Enterprise AI HR Assistant & Talent Intelligence Platform

> **Turn Natural Language Requests into Safe, Automated HR & Operations Workflows.**

NEXUS-AGENT LITE is an enterprise workforce automation platform built with **C# .NET 10 Web API**, **Entity Framework Core**, **SQL Server**, **React 19**, **Gemini 2.5 LLM Intelligence**, and **Python Automation**. 

It enables HR managers to interact with enterprise databases using conversational AI, automates employee onboarding and policy enforcement, and provides an end-to-end **Talent Acquisition, PDF Resume Extraction, and AI CV Screening pipeline**.

---

## 🌟 Key Features

### 1. 🤖 Conversational HR Assistant & Workflow Execution
* Plain English natural language command execution:
  * *"Onboard Ahmed Khan as a mid-level .NET developer in IT"*
  * *"Log Sufyan's sick day today and notify his team"*
  * *"Show me departments exceeding their allocated Q3 budget"*
* Automated action execution with fallback safety guards.

### 2. 💼 Job Requisition & Pipeline Management
* Full CRUD requisition lifecycle with compensation ranges, department tagging, and required competencies.
* Real-time counters showing total candidate applications stored in SQL Server.
* 1-click shareable public application link generator with clipboard copy.
* Interactive applicants drawer to view and inspect all submissions.

### 3. 🌐 Dedicated Public Candidate Portal (`http://localhost:3001`)
* **Department Filtering**: Filter active openings by unit (e.g. **`All Departments`**, **`IT`**, **`Marketing`**, **`HR`**, etc.) with live opening counters.
* **Role Transparency**: Detailed role descriptions, core responsibilities, required competencies, and a **4-Card Perks Grid** (Top Compensation, Hybrid/Remote, Health & Wellness, Learning Stipends).
* **PDF-Only Applications**: Strictly accepts `.pdf` documents (no raw text paste needed) with drag-and-drop or file browsing and instant file validation.
* **Vertical Layout**: Clear, natural top-to-bottom layout with job specifications on top and the candidate submission form directly below.

### 4. ✨ AI CV Screening with Embedded PDF Viewer
* **Embedded PDF Document Viewer**: Direct in-browser PDF viewer displaying the candidate's actual submitted resume document with 1-click PDF download.
* **Automated PDF Word Extraction**: Powered by **`UglyToad.PdfPig`**, the agent automatically extracts every word and token from candidate PDF pages on the backend and saves the text to the database.
* **View Extracted Words Switcher**: Recruiter toggle between the visual PDF document and the agent's parsed word stream (with exact word count).
* **Neural Match Scoring**: Gemini dynamically calculates match percentages, evaluates candidate fit against job requirements, and highlights ⭐ **Best Fit** candidates.
* **Dynamic Role-Specific Interview Questions**: Generates 5 technical interview questions based directly on the candidate's actual projects, claims, and identified skill gaps (no hardcoded questions).
* **1-Click Onboarding**: Direct action to approve candidate and create an employee record in the directory.

### 5. 🛡️ Human-in-the-Loop Approval Center
* Plan of Action review gate for high-risk operations (salary changes, terminations, department budget freezes).
* Visual diff tables and financial impact calculation before committing database changes.

### 6. 🔒 Cryptographic Audit Ledger & RBAC
* Read-only SQL Sandbox preventing destructive queries (`DROP`, `DELETE`).
* Immutable SHA-256 cryptographic audit ledger tracking every agent-executed action.

---

## 🚀 Quick Start Guide

### Prerequisites
* **.NET 10 SDK**
* **Node.js** (v20+) & **npm**
* **Python 3.12** (`py` launcher installed)
* **SQL Server** (LocalDB / Express / Developer Edition)

---

### Step 1: Start Backend API (Port 5160)
```powershell
cd backend/Nexus.Api
dotnet run --launch-profile http
```
> Runs ASP.NET Core API at **`http://localhost:5160`** (Swagger docs at `/swagger`)

---

### Step 2: Start HR Admin App & AI Assistant (Port 3000)
```powershell
cd frontend
npm run dev
```
> Open browser at **`http://localhost:3000`**

---

### Step 3: Start Public Candidate Application Portal (Port 3001)
```powershell
cd frontend
npm run dev:portal
```
> Open browser at **`http://localhost:3001`** *(Dedicated public portal with department filtering and PDF resume upload)*

---

### Step 4: Standalone Production HTML Application
The repository also includes a production-ready, single-file HTML/CSS/JS version with dark theme (`#0d1117` base) located at:
* File path: [`frontend/nexus-hr.html`](file:///d:/.NET%20PROJECTS/NEXUS%20AGENT%20LITE/frontend/nexus-hr.html)
* Or visit via dev server: **`http://localhost:3000/nexus-hr.html`**

---

## 📁 Repository Structure

```text
NEXUS AGENT LITE/
├── backend/
│   ├── Nexus.Api/             # ASP.NET Core Web API Controllers & Endpoints
│   │   ├── Controllers/       # AgentController, CvController, JobOpeningsController
│   ├── Nexus.Data/            # EF Core DbContext, Entities, and Gemini LLM Service
│   ├── Nexus.Agent/           # AI Intent Parsing & Autonomous Execution Engine
│   ├── Nexus.Tools/           # Automation Tools & SQL Sandbox
│   ├── Nexus.Security/        # RBAC & Cryptographic SHA-256 Audit Ledger
│   └── Nexus.Tests/           # Complete xUnit Test Suite (91 Tests)
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   │   ├── CandidateApplicationPortal.tsx  # Public Portal (Dept filter & PDF-only)
│   │   │   ├── CvCheckerView.tsx               # Embedded PDF Viewer & AI Screening
│   │   │   ├── JobOpeningsView.tsx             # Job Requisitions & Pipeline Manager
│   │   │   ├── AgentConsole.tsx                # Natural Language AI Chat Assistant
│   │   │   ├── EmployeesView.tsx               # Workforce Directory
│   │   │   └── DepartmentsView.tsx             # Corporate Budget Pools & Depts
│   │   ├── services/api.ts                     # Full TypeScript API Client
│   │   └── App.tsx                             # Port 3001 / Route Switcher
│   ├── public/
│   │   └── nexus-hr.html      # Standalone Single-File App (Public)
│   ├── nexus-hr.html          # Standalone Single-File App (Frontend Root)
│   ├── package.json           # Scripts: "dev" (3000) & "dev:portal" (3001)
│   └── vite.config.ts         # Vite Configuration & Backend Proxy
├── mock-portal/               # Python Mock Legacy HR Server (Port 8088)
├── docs/                      # Technical Architecture & Setup Documentation
└── README.md                  # System Documentation & Run Guide
```

---

## 🎬 Core Demonstration Scenarios

| Scenario | Location / URL | What Happens |
| :--- | :--- | :--- |
| **1. Department Filtered Career Portal** | `http://localhost:3001` | Candidate selects **IT** or **Marketing** filter; attaches candidate `.pdf` resume; application & PDF are submitted and stored in SQL database. |
| **2. Embedded PDF Screening & Word Extraction** | `http://localhost:3000` (CV Tab) | Agent displays the uploaded PDF in the embedded viewer, automatically extracts words with `UglyToad.PdfPig`, and provides a toggle to inspect extracted words. |
| **3. AI Fit Assessment & Dynamic Questions** | `http://localhost:3000` (CV Tab) | Click **"Run AI Evaluation"** to trigger multi-ring spinner, fit score reveal, and 5 dynamic role-specific interview questions generated by Gemini. |
| **4. Natural Language Onboarding** | `http://localhost:3000` (Console) | Command: `"Onboard Ahmed Khan as a mid-level .NET developer in IT"` triggers policy check, creates Action Plan, and provisions employee upon approval. |
| **5. Sick Leave & Email Dispatch** | `http://localhost:3000` (Console) | Command: `"Log Sufyan's sick day today and notify his team"` logs leave in SQL Server and dispatches real SMTP emails. |

---

## 🧪 Running Automated Unit Tests

```powershell
dotnet test backend/Nexus.slnx
```
*(All 91 unit and integration tests run cleanly with 0 failures!)*
