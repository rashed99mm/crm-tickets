# Frontend CRM UX Refactor Plan

**Spec:** [`../../specs/EPIC-01-US-101-frontend-crm-ux-refactor-design.md`](../../specs/EPIC-01-US-101-frontend-crm-ux-refactor-design.md)  
**Status:** Active  
**Layer:** Frontend only  
**Applications:** `admin-app`, `portal-app`, `common`

## Goal

Refactor the existing frontend UI/UX so it behaves like a usable Customer Support CRM workspace:
clear buttons, visible statuses and priorities, better backgrounds and density, readable charts,
responsive Arabic/English layouts, and workflow-focused screens for agents, managers,
administrators, and customers.

The design baseline is `stitch_smart_support_ticketing_crm`. The implementation should borrow its
support-workspace structure: ticket header/meta bands, customer 360 rails, AI assist panels, compact
queues, dashboard work lists, and management chart frames.

## Task Order

| Task | Scope | Criteria |
|---|---|---|
| 01 | Shared UX foundation | `AC-500` to `AC-503` |
| 02 | Shell, navigation, and layout | `AC-504` to `AC-506` |
| 03 | Core staff workflows | `AC-507` to `AC-511` |
| 04 | Portal, KB, reports, admin, and verification | `AC-512` to `AC-520` |

## Required UX Outcomes

- Ticket rows expose priority, status, assignee, customer, category, channel, SLA, escalation, and
  updated time without opening details.
- Ticket detail uses a workbench layout: metadata header, conversation/history, customer context,
  attachments/notes, permitted actions, and AI assist.
- Customer detail uses a 360 layout: contact profile, open tickets, interaction history, notes,
  attachments, and localized recovery states.
- Reports use chart frames with labels, legends, filters, table fallback, and visible empty/error
  content.
- AI usage is review-first: summary, categories, suggested replies, and KB solutions must be visible
  as suggestions with accept/reject or retry states.

## Working Rules

- Read the spec before implementing any task.
- Keep changes inside `frontend/` unless a task explicitly amends documentation.
- Use Angular 21 standalone components, signals, `OnPush`, Tailwind v4, shared `common` primitives, and
  logical direction utilities.
- Do not change backend contracts or invent data to populate a visual section.
- Each task must update its task file with actual commands run, results, and deviations before being
  marked complete.
- The current repository has a dirty working tree. Preserve unrelated user changes.

## Verification Commands

Run from `frontend/` when relevant:

```text
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test
```

## Definition Of Done

- Shared UI primitives cover the required button, badge, state, form, and chart shell behaviors.
- Staff and portal shells work on desktop, tablet, and mobile in English and Arabic.
- Ticket, customer, dashboard, KB, report, admin, and portal screens expose CRM-critical state
  without forcing unnecessary drill-in.
- Builds and focused tests pass, or every skipped/blocked command is recorded with a reason.
- Playwright visual review confirms no critical overlap, blank charts, invisible buttons, unreadable
  statuses, or horizontal overflow.
