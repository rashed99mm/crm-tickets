# FEAT-21 · Stitch-Faithful Right Rail — Backend

> **Plan:** `implementation-plan.md` in this folder.
>
> **Spec:** `docs/superpowers/specs/EPIC-11-US-701-feat-21-stitch-right-rail.md`.

## Status

In progress.

## Criteria delivered

- **AC-21.11** — `Summary` payload shape `{ text, sentiment }`; sentiment enum + classifier.
- **AC-21.12** — `Reply` payload shape `{ drafts: [...] }`; one call returns up to three drafts.
- **AC-21.13** — `Categories` payload shape unchanged.
- **AC-21.14** — `Solutions` payload shape unchanged.
- **AC-21.15** — error envelope codes unchanged (`ERR052`, `AI_THREAD_TOO_SHORT`, `AI_PROVIDER_FAILED`).
- **AC-21.16** — `AiSuggestionDto` shape unchanged (`Id, Kind, Payload, Status, Edited`).

## Test evidence

_(filled in by task records)_

## Gaps accepted

- Sentiment classification runs as a second provider call. If the AI provider is slow, the total
  summary round-trip is up to 2× the configured timeout. A single-call sentiment summarisation
  is feasible with a different prompt shape; deferred to a future slice.
- The `using CustomerSupport.Infrastructure.Ai;` import in `AiFeatures.cs` is a one-way Application
  → Infrastructure reference for the pure `AiJson` parser. The spec amendment records this trade-off;
  the dependency rule still holds for the rest of the system.

## Deviations

_(filled in by task records)_
