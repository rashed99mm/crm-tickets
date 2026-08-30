# Frontend UI/UX Gap Closure SDD

**Date:** 2026-08-29  
**Status:** Active  
**Source:** IDE gap report, Customer Support CRM scorecard  
**Scope:** Update the existing SDD program with one delivery record for every named gap, then close the gaps that already have usable backend contracts.

For full 100% backend + frontend + Stitch closure of every gap, use
[`EPIC-12-US-000-fullstack-gap-closure-sdd.md`](EPIC-12-US-000-fullstack-gap-closure-sdd.md). This document
records the smaller immediate UI slice; the full-stack SDD is the long-form execution authority.

## Problem

Several CRM screens looked complete but still contained static cards, decorative buttons, or fields
that were not wired to the backend. That creates false confidence: users can see controls that do
nothing, and reviewers cannot distinguish "not built yet" from "built but broken".

## Design Decision

Use three closure states for every gap:

- **Implemented:** this slice contains real code and a verification target.
- **Existing story:** the requirement is already covered by another SDD story and must be evidenced there.
- **Blocked:** the frontend cannot truthfully implement the behavior because the backend contract or data model is missing.

No placeholder-only UI may remain for a gap marked Implemented.

## Gap Register

| Area | Gap | State | SDD owner |
|---|---|---|---|
| Customer Management | WhatsApp, tags, plan/tier, verified email, manager, MRR, timezone, HQ | Blocked | New customer profile backend schema story |
| Customer Management | Customer tickets lane | Existing story | US-912 / ticket queue redesign |
| Customer Management | Note edit/delete | Blocked | Customer notes mutation story |
| Customer Management | Attachment rename | Blocked | Customer attachment metadata story |
| Ticket Management | Escalation rules config UI | Existing story | US-218 |
| Communication Channels | Chat session static mockup | Implemented | This slice + FEAT-24 task 09 |
| Communication Channels | Email, WhatsApp, SMS inboxes | Blocked | Channel inbox frontend after endpoint inventory |
| Agent Dashboard | Agent workspace, tasks, reminders, quick replies, collaboration, internal chat, presence | Blocked | Agent workspace backend+frontend epic |
| SLA & Automation | Auto-assignment rules UI | Existing story | US-220 |
| SLA & Automation | Business hours and holidays UI | Existing story | US-215 |
| SLA & Automation | Email/SMS alert config | Existing story | US-219 |
| Knowledge Base | Version history not rendered | Implemented | This slice + FEAT-31 |
| Knowledge Base | Category picker missing | Implemented | This slice + FEAT-31 |
| Knowledge Base | Insights card static | Implemented | This slice + FEAT-31 |
| AI Features | Suggested Reply button missing/unclear | Implemented | This slice + FEAT-21 |
| AI Features | AI panel KB article route 404 | Implemented | This slice + FEAT-21 |
| AI Features | Chat-session AI sidebar static | Implemented locally | This slice, backend AI remains blocked until chat sessions link to tickets |
| Customer Portal | Forgot password link | Implemented | This slice |
| Reports & Management | Export PDF/CSV | Partially implemented | Audit/user CSV in this slice; report export remains US-609 |
| Reports & Management | Dashboard trend/CSAT hardcoded | Existing story | US-606 / US-605 |
| Reports & Management | Per-agent drill-down | Existing story | US-604 follow-up |
| Security & Administration | User edit/department | Partially implemented | Department display and active toggle in this slice; edit/assign blocked by API |
| Security & Administration | Audit export | Implemented | This slice |
| Security & Administration | Profile notification/billing tabs, timezone/job title | Blocked | Profile preferences story |
| Integrations | Integration cards/API keys/buttons | Blocked | External API configuration UI story |
| Platform | Multi-branch UI | Existing story | US-310 |
| Platform | Multi-team UI | Blocked | Team admin frontend story |
| Platform | Branding form miswired | Implemented | This slice + US-314 |
| Platform | Branding runtime not applied after save | Implemented | This slice + US-314 |
| Platform | Logo upload missing input | Implemented | This slice + US-314 |
| Platform | Global language default | Blocked | Platform localization settings story |
| Platform | Multi-department tree | Existing story | US-907 |

## Implemented Acceptance Criteria

- **AC-GAP-01:** Chat session renders `ChatStore.messages()` and sends through `ChatStore.sendMessage`; no mock transcript remains.
- **AC-GAP-02:** Chat session close uses `ChatApi.closeSession`, and sidebar suggestions insert into the real composer.
- **AC-GAP-03:** Platform branding binds `logoUrl`, `primaryColor`, and `accentColor` to matching controls, accepts image file input, and applies saved branding through `BrandingStore`.
- **AC-GAP-04:** Platform settings render editable rows from `PlatformSettingApi`.
- **AC-GAP-05:** AI suggested articles route to `/kb-admin`, an existing admin route, not `/knowledge-base/:id`.
- **AC-GAP-06:** The AI reply action is visible as "Draft reply with AI".
- **AC-GAP-07:** KB create/edit supports category selection and persists it through `assignCategory`.
- **AC-GAP-08:** KB edit renders version history returned by `KbAdminApi.versions`.
- **AC-GAP-09:** KB insights derive totals and publish rate from loaded articles.
- **AC-GAP-10:** Audit log and users export the visible filtered data as CSV.
- **AC-GAP-11:** User rows expose activate/deactivate directly and show `departmentName` when the API returns it.
- **AC-GAP-12:** Admin and portal login screens expose a forgot-password support link until a reset-token backend story exists.

## Out Of Scope For This Slice

Backend schema changes, new task/canned-response/internal-chat entities, true chat-session AI over
ticket-linked context, report PDF generation, integration credential management, and branch/team CRUD
screens remain in their existing or newly identified stories.
