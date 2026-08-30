# EPIC-11 · AI features

| | |
|---|---|
| **Epic** | `EPIC-11` |
| **Priority** | P2 |
| **Stories** | 0 specified — backlog only |
| **Sprints** | 15 (slice S7) |

## Goal

Use AI to assist customers and support agents without making core CRM functionality dependent on AI
availability *(rule specification §8)*.

## Status: not specified — deferred, never stubbed

`B5` rules this out until an external model provider exists: a stub would misrepresent the
capability, and a customer-facing bot that answers wrongly is worse than no bot. The gate is legal
before it is technical — no personal data reaches an external provider without a recorded
data-processing decision (`PA-9`, `OQ-8`, `DEP-6`), which is why S7 sits last in the plan with the
longest time for that decision to clear.

Architectural principle already fixed (rule specification §11, §17.1): AI is an optional layer off
the ticket event stream —

```text
Ticket Event
    ├── Core CRM Workflow      must work with AI unavailable
    ├── SLA Processing         S2
    ├── Notification Processing
    └── Optional AI Processing  S7
```

Human control is a rule, not a preference: no AI-generated content reaches a customer and no AI
action changes ticket state without explicit human confirmation (`BR-19`).

## Reserved backlog (rule-file titles — unspecified by design)

US-087 Generate Ticket Summary · US-088 Suggest Agent Reply · US-089 Automatically Suggest Ticket
Category · US-090 Suggest Knowledge Base Solution · US-091 Answer Customer Through AI Chatbot (also
deferred indefinitely per BRD §6.3) · US-092 Transfer AI Conversation to Human Agent
