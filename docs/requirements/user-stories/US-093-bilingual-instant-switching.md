# US-093 · The interface is bilingual and switches instantly

| Field | Value |
|---|---|
| **Story** | `US-093` *(was `US-1.41`)* — rule proposal: *Change Application Language*; secondarily realizes Arabic/English interfaces (US-094/US-095) |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-10` Localisation](../delivery-plan.md#feat-10--localisation) |
| **Layer** | Frontend |
| **Ships with** | — Frontend-only. The server half - both languages in every response - shipped in FEAT-01. |
| **Actor** | Support Agent |
| **Priority** | P1 |
| **Sprint** | [4 — Contract hardening, localisation and the journey](../delivery-plan.md#sprint-4--contract-hardening-localisation-and-the-journey) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-12.1, FR-12.3, FR-12.4, BR-22 |
| **Spec criteria** | AC-63, AC-68 |
| **Depends on** | [US-106](./US-106-bilingual-message-catalogue.md) *(sprint 1)*, [US-038](./US-038-usable-ticket-list.md) |

## Story

**As an Arabic-speaking agent**, **I want** to switch language without losing my place, **so that** I can work in the language I think in.

## Business rules

- BR-22 — every response carries both languages; selection belongs to the client (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Text resolves through i18n (spec AC-63)

No user-facing string is hardcoded in a template; text resolves through the i18n mechanism, and the
document direction follows the active locale.

#### AC2 — Locale selects message, no refetch (spec AC-68)

The active locale selects `ar` or `en` from each response's `message` object; **no refetch occurs
when the language is switched**.

## SQL tables

None — frontend story. The server half (both languages in every `message`) is already proven by
US-106; nothing new is persisted.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-63 | Frontend (Vitest) | `planned` — no hardcoded strings | every template / grep or compile-time check for literal text | all text resolves through the i18n mechanism |
| TC-02 | AC-63 | Frontend (Vitest) | `planned` — direction | locale switched to Arabic / inspect `<html dir>` | `dir="rtl"` follows the locale |
| TC-03 | AC-68 | Frontend (Vitest + `HttpTestingController`) | `planned` | a response flushed with both messages / switch locale / inspect rendered text | displayed message flips `en` ↔ `ar` from the held response |
| TC-04 | AC-68 (no refetch) | Frontend (Vitest) | `planned` | switch language after data loaded / count HTTP requests | zero additional requests |

## Notes

The no-refetch property is a direct consequence of both languages travelling in every response, which is the decision recorded in ADR 0007. It is the commercial payoff of a choice that otherwise looks like wasted bytes.

The mechanism ships here; reviewed Arabic copy does not. The catalogue currently holds developer placeholders — see `PA-7` — and sprint 14 is where that is fixed.

## Open questions

- PA-7 — reviewed Arabic copy does not ship here; the catalogue currently holds developer
  placeholders until sprint 14.
  Tracked in [the register](../../product/05-assumptions-and-open-questions.md).

## Status evidence

Shipped — `TRANSLATIONS` dictionary, `TranslatePipe`/`LocalizePipe`, `LocaleStore.t()`, and the
`no-hardcoded-strings.spec.ts` guard that keeps `AC-63` true going forward. `bilingual-ui.spec.ts`
(8/8 passing, re-confirmed 2026-08-27) directly names both criteria: `AC63`/`AC68` tests cover
dictionary bilinguality, pipe re-render on switch, `dir=rtl`, reload persistence, and — the one
that matters per the plan's own notes — zero HTTP requests on language switch. Reviewed Arabic
copy does **not** ship here (`PA-7`, tracked separately, see `US-313`): the catalogue holds
developer-placeholder Arabic. See
`docs/superpowers/plans/EPIC-13-US-311-mvp-bilingual-ui/implementation-plan.md`.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
