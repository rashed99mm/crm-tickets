# FEAT-24..27 Communication Channels — record

**Plan:** [`implementation-plan.md`](implementation-plan.md)
**Spec:** [`EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`](../../specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md)
**Frontend spec:** [`EPIC-10-US-203-communication-channels-frontend.md`](../../specs/EPIC-10-US-203-communication-channels-frontend.md)
**Frontend plan:** [`frontend-implementation-plan.md`](frontend-implementation-plan.md)

## Status: partly implemented — this record was stale until 2026-09-02

**Correction.** This file claimed "planning only — nothing implemented" and "All 29 `CC-n` criteria
in the spec are unimplemented". Both were wrong, and had been wrong for some time: tasks 1–3 were
executed and their tests exist, named after the criteria they cover. The state below was rebuilt by
reading the code and the test suite, not by trusting this record or the spec header (which carried
the same error). The original claim is left visible in this paragraph rather than deleted, because
a record that quietly corrects itself teaches nobody anything.

The lesson worth keeping: this drifted because the record was written at planning time and never
touched again when the work landed. A task record is written **as the task completes**, per
`.claude/skills/sdd-workflow/SKILL.md`.

## Criteria delivered

Verified against the code on 2026-09-02.

| Feature | Criteria | Task | Status |
|---|---|---|---|
| Shared ingestion | `CC-1`–`CC-5` | Task 1 | **done** — `IngestInboundChannelMessage*`, 8 integration tests (`CC1_`…`CC4_`) |
| `FEAT-24` WhatsApp | `CC-6`–`CC-10` | Task 2 | **done** — sender, signed webhook, outbound reply; 13 tests (`CC5_`…`CC10_`) |
| `FEAT-25` SMS conversations | `CC-11`–`CC-13` | Task 3 | **partial** — `CC-13` (outbound reply) done; `CC-11`/`CC-12` **not started**, no SMS webhook transport exists |
| `FEAT-26` Live chat | `CC-14`–`CC-19` | Task 4 | **partial** — session/message entities, `ChatController` on both hosts, `ChatHub`; `CC-18` not started (and was impossible as specified — see spec `A18`) |
| `FEAT-27` Web forms | `CC-20`–`CC-23` | Task 5 | **not started** on the backend — the anonymous endpoint does not exist, though the frontend widget (task 11) is complete |
| Frontend (`CC-24`–`CC-26`) | — | tasks 07–11 | recorded complete |
| Cross-cutting security | `CC-27`–`CC-29` | Task 6 | **partial** — enforced on the WhatsApp path only, since it is the only inbound transport that exists |

### Added 2026-09-02 by the spec amendment (`FEAT-35`)

`CC-30`–`CC-50` — mock provider gateway in `cms-integration-gateway`, the `Channels:UseMocks`
toggle, inbound email, provider-faithful SMS inbound, the two client simulators, and the two
consolidations (`CC-48` one channel allow-list, `CC-49` one sender base). **Not started**; plan to
be written next.

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
