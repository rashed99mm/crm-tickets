# US-807 · One confirmation dialog, on every destructive action

| Field | Value |
|---|---|
| **Story** | `US-807` |
| **Epic** | [EPIC-09 Security & administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-34` Role & permission workbench](../delivery-plan.md#assessment-sprint-3--role--permission-workbench) |
| **Layer** | Frontend |
| **Ships with** | [US-806](./US-806-permission-workbench.md) — the workbench cannot ship without the queue this story adds (`AC-807.1`), because its Save prompt and its navigation prompt can be pending at the same time. Recorded single-layer: the server half of all three adopted actions already exists and is unchanged. |
| **Rule proposal** | — cross-cutting UX hardening; no rule-file counterpart |
| **Actor** | Administrator |
| **Priority** | P1 |
| **Sprint** | 21 — Role & permission workbench · Slice S9 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | NFR — usability; §8 administrative configuration |
| **Spec criteria** | AC-807.1 … AC-807.7 |
| **Depends on** | The existing `ConfirmationService` / `CsConfirmationHost` in `common`, already mounted at `shell.component.html:275` |

## Story

**As an administrator**, **I want** every action that takes something away to ask me first, in the
same dialog every time, **so that** a mis-click costs a keystroke instead of a support incident — and
so that I can dismiss that dialog with the keyboard like any other modal.

## Business rules

- A destructive action confirms before it is sent. Cancelling issues no request at all — not a
  request that is then undone.
- An action that *grants* or *restores* does not confirm. Activating a user is not destructive and
  must not be gated (spec `A12`).
- One dialog implementation, project-wide. A screen that rolls its own confirm is a screen whose
  confirm behaves differently under RTL, keyboard and screen readers.
- Every `confirm()` caller receives a result. A displaced request is queued, never dropped.

## Acceptance criteria

Criteria are cited from
[the spec](../../superpowers/specs/EPIC-09-US-806-permissions-workbench-and-global-confirmation.md),
not paraphrased. The spec is authoritative; if this file and the spec disagree, the spec is right and
this file is stale.

- **`AC-807.1`** — a second `confirm()` while one is pending: both callers get a result, the second
  dialog opens after the first resolves, no observable is left unresolved.
- **`AC-807.2`** — `Escape` closes the dialog and resolves `false`.
- **`AC-807.3`** — focus moves into the dialog on open (cancel control for `danger`) and returns to
  the trigger on close.
- **`AC-807.4`** — a request carrying `details` renders each as its own list item.
- **`AC-807.5`** — deactivating a user confirms; activating does not.
- **`AC-807.6`** — deactivating a department confirms.
- **`AC-807.7`** — deactivating an SLA policy confirms.

## SQL tables

None. Presentation-layer only; no endpoint changes.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-807.1 | Unit | `planned` | confirm() twice, resolve first | first caller gets its result, second dialog then opens, both complete |
| TC-02 | AC-807.2 | Component | `planned` | dialog open / Escape | closed, resolved `false` |
| TC-03 | AC-807.3 | Component | `planned` | open then close | focus inside on open, back on the trigger after close |
| TC-04 | AC-807.4 | Component | `planned` | request with three details | three list items under the message |
| TC-05 | AC-807.5 | Component | `planned` | Deactivate then cancel; Activate | no PUT on cancel; no dialog for activate |
| TC-06 | AC-807.6 | Component | `planned` | department Deactivate then cancel | no DELETE issued |
| TC-07 | AC-807.7 | Component | `planned` | SLA policy Deactivate then cancel | no DELETE issued |

## Notes

Two of these criteria fix live defects rather than adding polish, both found by reading the code on
2026-09-01 and recorded as the spec's Findings 1 and 2:

- `confirmation.service.ts:24-28` overwrites the pending request without resolving it, so the
  displaced caller's `Observable` never emits and never completes. Latent today because
  `kb-admin.component.ts:335` is the only caller; `US-806` makes it reachable.
- `confirmation-host.component.html` renders `role="alertdialog"` with `aria-modal="true"` and no
  Escape handler, no initial focus and no focus restoration.

`customer-detail.component.ts:225` keeps its bespoke inline confirm and `kb-admin.component.ts:335`
already uses the service; neither is migrated (spec `A12`).

## Open questions

None.

## Status evidence

`not started` — spec approved 2026-09-01, frontend plan written the same day, no code written.
Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
