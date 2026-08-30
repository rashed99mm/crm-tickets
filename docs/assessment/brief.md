# Assessment brief

**Received:** 2026-08-24
**Status:** Captured verbatim. Awaiting scope decomposition — see Interpretation notes.

## Verbatim brief

Customer Support CRM . Core Features

1. Customer Management
- Customer profiles
- Contact details
- Interaction history
- Notes and attachments

2. Ticket Management
- Create and track tickets
- Categories and priorities
- Assign tickets to agents
- Status and escalation
- Ticket history

3. Communication Channels
- Email
- WhatsApp
- Live chat
- SMS
- Web forms

4. Agent Dashboard
- Assigned tickets
- Customer information
- Tasks and reminders
- Quick replies
- Team collaboration

5. SLA & Automation
- Response and resolution targets
- Automatic assignment
- Escalation rules
- Alerts and notifications

6. Knowledge Base
- FAQs
- Help articles
- Solutions and guides
- Search

7. AI Features
- Ticket summaries
- Suggested replies
- Automatic categorization
- Suggested solutions
- AI chatbot

8. Customer Portal
- Submit tickets
- Track requests
- View history
- Access FAQs
- Submit feedback

9. Reports & Management
- Ticket reports
- SLA performance
- Agent performance
- Customer satisfaction
- Management dashboards

10. Security & Administration
- Users and roles
- Permissions
- Audit logs
- System configuration

11. Integrations
- APIs
- ERP
- Email, SMS & WhatsApp
- External systems

12. Platform
- Arabic & English
- Web and mobile friendly
- Multi-department
- Multi-branch
- Custom branding

## Interpretation notes

The section above is the client's words. Everything below is our reading of them, and may not
contradict it. Where it seems to, the brief wins and the conflict gets raised.

### Scope observation (raised 2026-08-24)

The brief describes **twelve feature areas constituting a complete commercial support
platform** — five communication channels, an AI subsystem, a customer-facing portal, reporting,
ERP integration, bilingual RTL support and multi-branch tenancy. That is a multi-team,
multi-quarter product, not a single deliverable.

It cannot be specified in one spec, and attempting all of it produces twelve shallow features
where the assessment rewards depth: correctness, tests, edge cases, security, and decisions that
can be defended. **The brief is therefore read as the product vision, with the assessment
delivering one vertical slice of it end to end**, plus a documented decomposition of the
remainder.

### Agreed decomposition (2026-08-24)

Eight slices, each getting its own spec → plan → implement cycle. **S1 is the assessment
deliverable**; the rest document the path without being built.

| Slice | Content | Brief areas |
|---|---|---|
| **S1 — Ticket lifecycle** | Auth + roles, customers with notes and attachments, tickets with category/priority/status/assignment, ticket history, agent views | 1, 2, 4 (part), 10 (part) |
| S2 — SLA & automation | Response/resolution targets, auto-assignment, escalation rules, alerts | 5 |
| S3 — Customer portal | Submit, track, history, feedback, web-form channel | 8, 3 (part) |
| S4 — Knowledge base | FAQs, articles, solutions, search | 6 |
| S5 — Email channel | Inbound and outbound email in depth | 3 (part), 11 (part) |
| S6 — Reporting | Ticket, SLA, agent and satisfaction reports; dashboards | 9 |
| S7 — AI assist | Summaries, auto-categorisation, suggested replies and solutions | 7 |
| S8 — Platform | Arabic/English translation and RTL, branding, multi-department, multi-branch | 12 |
| _Deferred indefinitely_ | WhatsApp, SMS, live chat, ERP connectors, AI chatbot, native mobile | 3, 7, 11 |

S1 is first because every other slice needs a ticket to exist, and because it exercises all nine
rubric criteria on its own. Its spec is `docs/superpowers/specs/2026-08-24-ticket-lifecycle-design.md`.

### Assumptions

Each is a question that could not be asked, written so it can be proven wrong. The full list
governing S1 is in that spec; these are the ones that shape the whole product reading.

- **B1.** The brief is a product vision, not a single deliverable. The assessment delivers one
  vertical slice end to end plus this decomposition.
- **B2.** Depth on one slice scores better than breadth across twelve, because six of the nine
  rubric criteria measure depth (correctness, testing, security, edge cases, maintainability,
  ownership).
- **B3.** Agents are internal staff created by an administrator; there is no public
  self-registration anywhere in the product as described.
- **B4.** "Multi-department" and "multi-branch" are organisational grouping, not per-tenant
  database isolation.
- **B5.** The AI features (area 7) assume an external model provider and are not viable without
  one; they are deferred rather than stubbed, because a stub would misrepresent the capability.

### Time budget

Two to three working days, stated 2026-08-24. This is **less than the agreed S1 scope needs** —
full ASP.NET Core Identity plus notes, attachments and customer CRUD is realistically four to
five days. The scope was chosen deliberately over a narrower recommendation. The spec's build
order is therefore priority-ordered with explicit cut lines so that running out of time removes
one whole feature cleanly rather than leaving several half-finished.

### Ambiguities

- **"Escalation" appears in both area 2 and area 5.** Read as: area 2 owns the ticket's
  escalation *state*, area 5 owns the *rules* that change it.
- **"Web and mobile friendly"** is read as a responsive web application, not native mobile apps.
  No native app fits an assessment timebox under any reading of this brief.
- **"APIs" under Integrations** is read as this application's own HTTP API being fit for external
  consumption, not as building connectors to unspecified third-party systems.
- **"Multi-department" and "Multi-branch"** are read as organisational grouping of agents and
  tickets, not as hard tenant isolation with a database per branch.
- **"Interaction history" (area 1) and "Ticket history" (area 2)** overlap. Read as: ticket
  history is the audit trail of one ticket's changes; interaction history is the customer's
  cross-ticket timeline.

### Out of scope

Out of scope for the **assessment deliverable (S1)**, each assigned to a later slice above rather
than dropped. Listed so an assessor reads a boundary rather than an omission:

Cross-ticket interaction timeline · tasks and reminders · quick replies · team collaboration ·
SLA targets and escalation rules · automatic assignment · notifications and alerts · all five
communication channels · knowledge base and search · all AI features · customer portal · reports
and dashboards · system-wide audit log beyond ticket history · ERP and external integrations ·
multi-department and multi-branch · custom branding · Arabic translation · native mobile apps.

The S1 spec repeats this list with its acceptance criteria, so the boundary is visible from either
document.
