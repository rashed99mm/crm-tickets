# FEAT-21 · Stitch-Faithful Right Rail — Frontend

> **Plan:** `implementation-plan.md` in this folder.
>
> **Spec:** `docs/superpowers/specs/EPIC-11-US-701-feat-21-stitch-right-rail.md`.

## Status

In progress.

## Criteria delivered

- **AC-F9** — sentiment chip on the Context Summary card.
- **AC-F10** — Suggested Replies card lists drafts; Insert writes to the composer.
- **AC-F11** — chrome-free "AI Assistant" header band.
- **AC-F12** — four-card layout (Summary, Replies, KB, Categories).
- **AC-F13** — composer toolbar's Draft with AI.
- **AC-F14** — `TicketMessagesComponent.insertDraft()` public method.

## Test evidence

_(filled in by task records)_

## Gaps accepted

- The Insert write is parent-to-child via a template ref, not a shared service. The two
  components remain decoupled beyond the one (ticketId, insert) handshake.
- `CsErrorState` is reused as the per-card failure UI; the previous shape's "one error block
  for the whole panel" is now per-card, which is what the mockups show.

## Deviations

_(filled in by task records)_
