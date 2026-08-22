# NEXUS-AGENT LITE — Architectural Decision Records (ADR)

## ADR 001: Local Open-Source LLM Architecture
- **Date**: 2026-08-18
- **Status**: Accepted
- **Context**: The project explicitly forbids using cloud AI APIs (OpenAI, Claude, Gemini, Azure OpenAI).
- **Decision**: All natural language reasoning and intent parsing must use a local open-source LLM (e.g. Llama 3 / Mistral / Qwen via Ollama or LocalAI runtime).
- **Consequence**: Zero cloud dependencies, complete privacy, deterministic tool invocation schema, pluggable backend provider interface for changing local models seamlessly.

---

## ADR 002: Modular Monolith vs. Microservices
- **Date**: 2026-08-18
- **Status**: Accepted
- **Context**: Hackathon build target requires fast iteration and simple local orchestration without microservice deployment overhead.
- **Decision**: Standardize on a C# .NET 9 modular solution (`Nexus.Api`, `Nexus.Agent`, `Nexus.Tools`, `Nexus.Security`, `Nexus.Data`).
- **Consequence**: Low latency in-process method execution, shared memory state when needed, easy single-command startup.

---

## ADR 003: Backend Tool Authority & SQL AST Guardrails
- **Date**: 2026-08-18
- **Status**: Accepted
- **Context**: LLMs cannot be trusted to directly execute generated SQL or command strings without validation.
- **Decision**: `Nexus.Security` acts as an mandatory gatekeeper. All LLM-generated SQL queries are parsed into AST or validated against strict token rules, checked for forbidden DDL/DML, verified against table/column permissions, and capped with row limits & timeouts before execution.
- **Consequence**: Prevents SQL injection, unauthorized table access, data destruction, and server exhaustion.

---

## ADR 004: Gated Human-in-the-Loop Execution (Plan of Action)
- **Date**: 2026-08-18
- **Status**: Accepted
- **Context**: High-risk or bulk updates (e.g., updating multiple employee salaries) require user transparency and explicit authorization.
- **Decision**: Operations classified as HIGH or CRITICAL risk generate a structured Plan of Action (POA) containing diffs, old/new values, and total impact, halting execution until the human user confirms in the UI.
- **Consequence**: Total safety against accidental mass modifications while allowing low-risk queries to run autonomously.
