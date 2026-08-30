# Frontend CRM UX Refactor

**Date:** 2026-08-28  
**Status:** Active  
**Type:** Cross-cutting frontend refactor  
**Applications:** `admin-app`, `portal-app`, `common`  
**Related product baseline:** [`../../customer-support-crm-sdd-specification.md`](../../customer-support-crm-sdd-specification.md)  
**Related visual baseline:** [`EPIC-11-US-701-mockup-faithful-ui.md`](EPIC-11-US-701-mockup-faithful-ui.md)

## Problem

The frontend already exposes most Customer Support CRM capabilities, but the experience is still
closer to a feature checklist than an operational support workspace. Buttons, backgrounds, status
visibility, dashboard charts, tables, forms, and portal flows need a coherent UX pass so agents,
supervisors, administrators, and customers can understand priority, ownership, SLA risk, and next
actions at a glance.

This refactor makes the UI reflect the CRM specification rather than individual feature screens:
customer management, ticket management, communication channels, dashboards, SLA automation,
knowledge base, AI assist, portal, reporting, administration, integrations, and platform needs must
feel like one product.

## Goals

- Make the first screen after sign-in immediately useful for support work.
- Make ticket urgency, SLA risk, ownership, status, and channel visible without opening every row.
- Make primary actions easy to find and secondary/destructive actions visually distinct.
- Make dashboards and reports readable through real chart components and clear empty/loading/error
states.
- Make customer and ticket detail screens support repeated daily work: scan, reply, assign, resolve,
escalate, add notes, use AI, and inspect history.
- Keep Arabic/English, RTL, responsive layout, permissions, and existing API contracts intact.
- Use the existing Angular 21 standalone architecture, `common` UI primitives, signals, Tailwind v4
tokens, and route structure.

## Non-goals

- No backend endpoint, migration, entity, or permission changes.
- No new CRM business rules hidden in the UI.
- No replacement of Angular, Tailwind, the shared `common` library, or existing API services.
- No marketing landing page for the staff app; the staff app remains a work surface.
- No fabricated operational data. Missing backend data must render as unavailable, empty, or
disabled, not as real metrics.

## Personas And UX Outcomes

| Persona | Needed outcome |
|---|---|
| Support Agent | Knows what to work next, sees assigned tickets, customer context, channel history, SLA risk, suggested replies, and next actions. |
| Team Lead | Sees queue health, unassigned work, escalations, overloaded agents, and reassignment controls. |
| Support Manager | Reviews SLA, volume, agent performance, CSAT, and trends through readable charts and filterable reports. |
| Administrator | Manages users, roles, permissions, departments, branches, SLA policies, integrations, audit logs, and branding with clear save/cancel states. |
| Customer | Submits tickets, tracks request history, reads FAQs, replies, and submits feedback on mobile and desktop. |

## Design Principles

- **Work-first layout.** Staff pages prioritize queues, actions, metadata, and state. Avoid oversized
  decorative heroes inside the authenticated app.
- **Stitch-inspired workspace.** Use the `stitch_smart_support_ticketing_crm` references as the
  visual baseline for CRM page composition: header/meta bands, three-column support workspaces,
  AI-assist side rails, customer 360 context rails, bento report panels, compact tables, and clear
  action clusters.
- **Visible hierarchy.** One primary action per screen; secondary, ghost, and destructive actions
  have consistent styling.
- **Semantic color.** Status, priority, SLA risk, channel, and escalation use distinct tokens with
  accessible contrast.
- **Operational density.** Tables, filters, dashboards, and rails should be compact enough for daily
  use without becoming cramped.
- **Bilingual by design.** Use logical spacing/direction utilities, translation keys, and mirrored
  layouts for Arabic.
- **Honest state.** Loading, empty, error, permission-denied, unavailable, and disabled states are
  visible and consistent.

## Stitch Reference Mapping

| Stitch reference | CRM screens to influence | Required UX translation |
|---|---|---|
| `ticket_detail_chatbot` | `admin-app` ticket detail, ticket messages, AI panel | Full-width ticket header band, status/priority/SLA/channel metadata strip, central conversation/history, right AI assist rail, visible retry states for failed load or failed action. |
| `ai_powered_agent_workspace` | dashboard, ticket queue, ticket detail | Agent-first layout with assigned work, active conversation, quick actions, suggested replies, summaries, categories, and solution citations grouped by task urgency. |
| `customer_360_history` | customer detail, notes, attachments, interaction history | Customer profile/contact rail, central timeline, attachment evidence list, open ticket context, localized empty/error states, clear upload/download/remove affordances. |
| `management_analytics_sla_performance` | reports and management dashboard | KPI band, readable chart frames, legends, filters, SLA breach/warning/healthy color treatment, and table fallback for every chart. |

## Error, Reload, And Recovery Model

- Every route-level data load must render exactly one of loading, loaded, empty, or error.
- Every error state must expose a retry action that calls the same load path and does not require a
  browser refresh.
- Mutating actions must show busy state on the triggering button and preserve the error message when
  the screen reloads to recover stale data.
- Report/chart regions must never show a blank canvas. Loading, empty, unavailable, and API failure
  states must occupy the chart frame.
- Upload/download/remove failures must be scoped to the attachment panel so they do not imply the
  whole customer record failed.
- Permission failures must keep the page readable and make forbidden actions unavailable or clearly
  refused.

## AI Usage Model

- AI surfaces are assistive and must never hide the source ticket/customer data.
- AI ticket summaries, suggested replies, automatic categorization, suggested KB solutions, and
  chatbot actions must live in a recognizable assistant rail or panel.
- AI output must show pending, accepted, rejected, edited, error, and retry states where the backend
  exposes them.
- Suggested KB solutions must link to existing article detail routes and display unavailable state
  when no citation data exists.
- The UI must not invent AI confidence, sentiment, model name, or generated text when the API does
  not provide it.

## Functional Surface Map

| CRM feature area | Frontend surfaces |
|---|---|
| Customer Management | `customers` list/create/detail, customer contact cards, notes, attachments, interaction history |
| Ticket Management | ticket queue, ticket create, ticket detail, assignment, status, escalation, priority, history |
| Communication Channels | ticket messages, chat queue/session, web form, channel indicators for email/WhatsApp/live chat/SMS/web |
| Agent Dashboard | dashboard work queue, assigned tickets, reminders/tasks placeholders, quick replies, collaboration states |
| SLA & Automation | SLA policy admin, SLA timers, warning/breach banners, auto-assignment/escalation explanations |
| Knowledge Base | staff KB admin, portal KB browse/search, suggested solutions in ticket/AI areas |
| AI Features | AI assistant action, ticket summary, suggested reply/category/solution panels, chatbot surfaces |
| Customer Portal | home, auth, dashboard, submit ticket, my tickets, ticket detail/reply, feedback/survey |
| Reports & Management | ticket volume, SLA performance, agent performance, live queue, CSAT reports |
| Security & Administration | users, roles/permissions, audit log, settings, guarded navigation and disabled actions |
| Integrations | external configuration surfaces and channel/provider health/status cards where existing routes exist |
| Platform | Arabic/English switching, responsive shell, departments, branches, branding tokens |

## Acceptance Criteria

### Shared UI Foundation

- **AC-500.** Given any staff or portal screen renders, when buttons are inspected, then primary,
  secondary, ghost, icon-only, danger, loading, and disabled states are visually distinct and
  accessible by name.
- **AC-501.** Given status, priority, SLA, channel, and escalation are shown, when the user scans a
  table, card, chart, or detail header, then semantic badges use consistent color, text, and icon
  treatment across both apps.
- **AC-502.** Given a card, table, dialog, drawer, form, or report region renders, when compared
  against the theme contract, then it uses shared tokens from `common/src/styles/theme.css` and shared
  UI components where a reusable primitive exists.
- **AC-503.** Given any form action is pending, succeeds, fails, or is not permitted, when the state
  changes, then the visible button and feedback state clearly communicate what happened without
  layout shift.

### Staff Shell And Navigation

- **AC-504.** Given a staff user signs in, when the shell loads, then dashboard, tickets, customers,
  KB, chat, reports, administration, settings, language, notifications, profile, sign out, and AI
  assistant surfaces are discoverable according to role permissions.
- **AC-505.** Given the shell renders on desktop, tablet, and mobile, when the user navigates, then
  active route, collapsed sidebar, mobile drawer, topbar, notifications, and AI action remain visible
  and do not overlap content.
- **AC-506.** Given Arabic is selected, when the shell and routed screens render, then navigation,
  drawers, rails, icons, table controls, charts, and action bars mirror correctly using logical
  layout utilities.

### Core CRM Workflows

- **AC-507.** Given the dashboard loads for an agent, when work exists, then assigned tickets, SLA
  risk, unassigned tickets, reminders/tasks, quick replies, team collaboration, and recent activity
  are visually prioritized for next action.
- **AC-508.** Given the ticket queue loads, when tickets exist, then category, priority, status,
  assigned agent, customer, channel, SLA risk, escalation, and updated time are visible in the row or
  mobile item without opening ticket detail.
- **AC-509.** Given ticket detail loads, when the agent reviews a ticket, then customer context,
  conversation timeline, history, attachments, notes, SLA banner, assignment, status transitions,
  quick replies, and AI suggestions are arranged for fast triage and reply.
- **AC-510.** Given customer detail loads, when an agent views the profile, then contact details,
  open tickets, previous interactions, notes, attachments, and audit-sensitive actions are clearly
  separated.
- **AC-511.** Given a staff user creates or edits a record, when validation fails, then each invalid
  field displays a localized message and the primary action remains easy to locate.

### Portal And Knowledge Base

- **AC-512.** Given a customer uses the portal, when they submit, track, reply to, or review a
  ticket, then the flow works comfortably on mobile and shows status/history without staff-only
  controls.
- **AC-513.** Given the knowledge base is shown to staff or customers, when articles, FAQs, search,
  categories, and suggested solutions render, then content hierarchy and empty states make search and
  self-service clear.

### Reporting And Management

- **AC-514.** Given a manager views reports, when ticket volume, SLA performance, agent performance,
  CSAT, or live queue data is available, then charts are readable, labeled, filterable, and paired
  with tabular fallback data.
- **AC-515.** Given a report has no data or fails to load, when the screen renders, then the chart
  area shows an explicit empty/error state and does not leave a blank canvas.
- **AC-516.** Given admin pages render, when users, permissions, audit logs, settings, departments,
  branches, SLA policies, and integrations are managed, then risky actions require clear affordances
  and normal edits use consistent dialogs/forms.

### Verification

- **AC-517.** Given the refactor is complete, when `npx ng test common --watch=false`,
  `npx ng test admin-app --watch=false`, and `npx ng test portal-app --watch=false` run, then all
  affected component and i18n tests pass.
- **AC-518.** Given the refactor is complete, when `npx ng build admin-app` and
  `npx ng build portal-app` run, then both builds complete successfully.
- **AC-519.** Given visual verification runs, when Playwright captures staff and portal surfaces at
  375px, 768px, 1280px, and 1920px, then no critical text overlap, blank chart, hidden primary
  action, unreadable badge, or horizontal page overflow remains.
- **AC-520.** Given the refactor is reviewed, when deviations from existing mockups/specs are found,
  then they are recorded in the execution plan instead of silently accepted.

## Implementation Approach

1. Refactor shared UI primitives first: buttons, icons, badges, cards, dialogs, forms, empty/error
   states, chart containers, and action bars.
2. Stabilize the staff and portal shells: navigation, topbar, mobile drawer, layout spacing, role
   visibility, notifications, language switcher, and assistant entry point.
3. Refactor core operational screens: dashboard, ticket queue, ticket detail, customer detail, and
   record creation/editing.
4. Refactor reports, knowledge base, administration, portal, and integration settings.
5. Harden with i18n, RTL, accessibility, responsive, build, and Playwright visual checks.

## Risks

- The current working tree contains many unrelated modified/untracked files; implementation must not
  revert or normalize unrelated work.
- Some designed regions may not have backend data. The UI must show unavailable states instead of
  inventing values.
- Report charts may need dependency confirmation. If no chart library is already present, choose a
  light, Angular-compatible option in the task before implementation.
- Hardcoded strings and physical direction classes may fail existing tests; new UI must use the
  translation catalog and logical utilities from the start.
