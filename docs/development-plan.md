# NEXUS-AGENT LITE — Development Roadmap & Phased Execution Plan

NEXUS-AGENT LITE development is structured into incremental, testable phases suitable for a fast-paced hackathon schedule.

---

## Phased Overview

| Phase | Title | Key Deliverables | Status |
|-------|-------|------------------|--------|
| **Step 01** | **Project Foundation** | Solution setup, project references, Vite frontend, Python structure, documentation. | **COMPLETED** |
| **Step 02** | **Database & Data Layer** | Entity Framework Core setup, domain models (Employee, Department, Policy, AuditLog, Expense), SQL Server migration & seed data. | Pending |
| **Step 03** | **Security & Guardrail Engine** | Permission matrix, Risk Evaluator, Safe SQL AST validator, Read-Only enforcement. | Pending |
| **Step 04** | **Tool Framework & Registered Tools** | Tool registry, SQL Tool, Policy RAG Tool, Python Playwright Tool, Mock SAP Tool, Email Tool. | Pending |
| **Step 05** | **Agent Engine & Intent Parser** | Local open-source LLM runtime connector, Intent classification, Multi-step Planner, POA generator. | Pending |
| **Step 06** | **Backend API & WebSockets** | Web API controllers, execution pipeline orchestration, real-time activity feed streaming. | Pending |
| **Step 07** | **Frontend UI & Interactive Feed** | React chat interface, Plan of Action confirmation modal, Safe execution trace stream. | Pending |
| **Step 08** | **Hackathon Demos & End-to-End Validation** | Primary onboarding flow (Ahmed Khan) and 3 secondary demo scenarios. | Pending |

---

## Demo Scenarios

1. **Primary Demo (Onboarding Ahmed Khan)**:
   - Natural Language Request: *"Onboard Ahmed Khan as a mid-level .NET developer in IT according to company policy. Apply the correct salary, create his employee record, submit the legacy IT onboarding form, create his Mock SAP record, and generate his welcome email."*
   - Verification: End-to-end execution across DB, Playwright, Mock SAP, and Email with audit log output.

2. **Secondary Demo 2 (Budget Compliance)**:
   - Query: *"Show me departments exceeding their allocated Q3 budget."*
   - Verification: Safe SQL generation, AST validation, read-only query execution, and summary response.

3. **Secondary Demo 3 (Expense Policy Validation)**:
   - Query: *"Check whether Ahmed's latest expense complies with company policy."*
   - Verification: Policy PDF retrieval, rule matching, expense record check, compliance result.

4. **Secondary Demo 4 (Bulk Salary Increase & POA Approval)**:
   - Request: *"Increase salaries of all IT developers by 10%."*
   - Verification: Plan of Action generation (impacted rows, old/new values), human confirmation prompt, gated tool execution upon approval.
