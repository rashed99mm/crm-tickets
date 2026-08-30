# FEAT-24..27 Communication Channels — record

**Plan:** [`implementation-plan.md`](implementation-plan.md)
**Spec:** [`EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`](../../specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md)
**Frontend spec:** [`EPIC-10-US-203-communication-channels-frontend.md`](../../specs/EPIC-10-US-203-communication-channels-frontend.md)
**Frontend plan:** [`frontend-implementation-plan.md`](frontend-implementation-plan.md)

## Status: planning only — nothing implemented

This folder exists to satisfy the SDD gate ahead of implementation, at explicit instruction to stop
after spec + plan + tasks. **No task below has been executed. No test has been run. No migration
has been created.** The "Ships / doesn't ship" table exists so the next person picking this up
starts from an honest state, not an assumed one.

## Criteria delivered

None yet. All 29 `CC-n` criteria in the spec are unimplemented.

| Feature | Criteria | Task | Status |
|---|---|---|---|
| Shared ingestion | `CC-1`–`CC-5` | Task 1 | not started |
| `FEAT-24` WhatsApp | `CC-6`–`CC-10` | Task 2 | not started |
| `FEAT-25` SMS conversations | `CC-11`–`CC-13` | Task 3 | not started |
| `FEAT-26` Live chat | `CC-14`–`CC-19` | Task 4 | not started |
| `FEAT-27` Web forms | `CC-20`–`CC-23` | Task 5 | not started |
| Frontend (`CC-24`–`CC-26`) | — | not tasked | blocked on backend landing first (SDD gate) |
| Cross-cutting security | `CC-27`–`CC-29` | Task 6 | not started |

## Frontend Task Map

These are planning artifacts only. No frontend task has been executed, tested, or committed.

| Surface | Criteria | Task | Status |
|---|---|---|---|
| Message timeline channel rendering | `FB-1` / `CC-24` | [Task 07](tasks/task-07-cc24-message-timeline-channels.md) | completed |
| Agent live-chat waiting queue | `FB-2` / `CC-25` | [Task 08](tasks/task-08-cc25-live-chat-queue.md) | completed |
| Agent live-chat transcript | `FB-3` / `CC-26` | [Task 09](tasks/task-09-cc26-agent-chat-transcript.md) | completed |
| Anonymous customer live chat | `FB-4`–`FB-5` / `CC-14`, `CC-16` | [Task 10](tasks/task-10-cc14-cc16-customer-live-chat-widget.md) | completed |
| Anonymous web-form widget | `FB-6`–`FB-9` / `CC-20`–`CC-23` | [Task 11](tasks/task-11-cc20-cc23-anonymous-web-form-widget.md) | completed |
| Frontend evidence gate | all frontend criteria | [Task 12](tasks/task-12-frontend-evidence-gate.md) | completed |

## Gaps accepted

- **The business decisions the original deferral named are still open** (`A11`/`OQ-CC-1..3`): no
  WhatsApp Business account is purchased or verified, no live-chat staffing roster exists, no
  CAPTCHA provider is chosen. This plan can be executed against a sandbox/mock provider; it cannot
  be deployed to production customers until those are resolved by the business, not by engineering.
- **`FEAT-14` (conversation record) is itself listed as backend-done/frontend-partial** in the
  delivery plan at the time this spec was written — Task 1 assumes `TicketMessage` exists as
  designed there. If `FEAT-14` changes shape before this is executed, Task 1 is the one to re-check
  first.
- **`FEAT-22` (customer portal, `US-404`)** may build its own authenticated ticket-submission path
  before this plan's Task 5 runs. `A4` in the spec already addresses the overlap; if `FEAT-22` lands
  first, Task 5 should be re-scoped to add only the anonymous entry point on top of its existing
  application-layer command rather than duplicating validation.

## Next step

Nothing in this folder should be picked up for implementation until the business decisions in `A11`
are at least scheduled, and until whichever of `FEAT-14`/`FEAT-22` this plan depends on has settled.
When work starts, execute Task 1 first — everything else in this plan depends on it — and update
this table and each task file from what actually ran, not from this plan's intent.
