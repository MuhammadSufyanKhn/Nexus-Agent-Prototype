# AI Infrastructure & Intent Parsing Specification

This document describes how **NEXUS-AGENT LITE** parses natural language prompts into structured execution payloads.

---

## 1. Intent Parsing Architecture

```
[User Text Prompt] ──> [LLM / Gemini API] ──> [JSON Payload] ──> [Deterministic Fallback]
```

When a user submits a prompt, `IntentParser.cs` extracts the core intent type, target entities, and parameters.

### Supported Intent Types

| Intent Enum | Purpose | Risk Level |
| :--- | :--- | :--- |
| `EMPLOYEE_ONBOARDING` | Multi-system onboarding workflow | High |
| `LEAVE_CREATE` | Log employee sick leave or PTO | Low |
| `EMPLOYEE_TRANSFER` | Department or role transfer | High |
| `EMPLOYEE_DELETE` | Permanent employee removal | Critical |
| `BUDGET_UPDATE` | Department budget allocation | High |
| `BUDGET_ANALYSIS` | Natural language SQL budget queries | Low |
| `POLICY_READ` | Search and check HR policies | Low |

---

## 2. Intent Parsing & Fallback Strategy

1. **Primary AI Parsing**: Sends prompt to Gemini API / Local LLM (`ILLMService.GenerateJsonAsync<ParsedIntentResult>()`) requesting a structured JSON schema.
2. **Deterministic Rule Fallback (`RuleBasedFallbackParse`)**: If LLM parsing is offline or malformed, C# regex patterns extract key entities (names, departments, salary numbers, dates) to ensure zero downtime.

---

## 3. Entity & Parameter Extraction

* **Employee Name Extraction**: Matches known names, action verb patterns (`log <Name> sick day`, `onboard <Name>`), and strips prefixes (`Log`, `Employee`).
* **Department Normalization**: Normalizes variants (`IT`, `HR`, `Finance`, `Operations`, `R&D`).
* **Financial Calculations**: Parses shorthand amounts (`50k` → `50,000.00`, `1b` → `1,000,000,000.00`).
