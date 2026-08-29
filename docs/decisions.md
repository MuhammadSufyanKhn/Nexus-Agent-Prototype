# Architecture Decision Records (ADRs)

This document tracks technical decisions made during the development of **NEXUS-AGENT LITE**.

---

## ADR 001: Human-in-the-Loop Pre-Execution Gate
* **Status**: Accepted
* **Decision**: All high-risk mutations (`EMPLOYEE_ONBOARDING`, `EMPLOYEE_UPDATE`, `EMPLOYEE_DELETE`, `BUDGET_UPDATE`) generate an Action Plan and pause in state `WaitingForApproval` with **zero database changes** occurring prior to human admin confirmation.

---

## ADR 002: Dual Recipient Email Subsystem
* **Status**: Accepted
* **Decision**: All automated notification emails (such as sick day alerts and welcome emails) dispatch directly to **both** `nexusagent.notifications@gmail.com` and the employee's database email address. Placeholder `.local` domain envelope recipients are filtered out to prevent SMTP bounce errors.

---

## ADR 003: SQL Sandbox Read-Only Restrictions
* **Status**: Accepted
* **Decision**: Natural language SQL analytics queries are strictly restricted to read-only `SELECT` statements with `TOP 100` row caps and a 5-second timeout.

---

## ADR 004: Persistent SQL Server Schema Initializers
* **Status**: Accepted
* **Decision**: Database initializers use `NOT EXISTS` conditions for schema updates so user-allocated budgets and updated records persist cleanly across server restarts.
