# ADR 0001 — Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-08-23

## Context

This project is graded in part on whether decisions can be *explained* — the rubric's
"Technical Understanding & Ownership" criterion asks for someone who can justify a design,
debug it, and adapt it, rather than recite what a tool produced.

Decisions made in conversation are lost by the next day. When an assessor asks "why MediatR
here?" or "why is validation in a pipeline behavior rather than the controller?", the answer
has to be reconstructed from memory, and reconstructed answers sound like rationalisations
because that is what they are.

## Decision

Every non-obvious technical decision gets a short record in `docs/adr/`, written *at the time
the decision is made*, using `template.md`.

A decision is "non-obvious" if a competent engineer could reasonably have chosen otherwise.
Picking `xUnit` over `NUnit` is a coin toss and needs no ADR. Choosing Clean Architecture's
four projects over a vertical-slice layout is a real trade-off and needs one.

Each record must name the alternatives that were considered **and why they lost**. An ADR
listing only the winning option documents nothing — the reasoning lives entirely in the
comparison.

## Consequences

- There is a written, dated trail of reasoning to revisit under questioning.
- Superseding a decision means writing a new ADR that links the old one, not editing history.
  The wrong turns stay visible, which is the point.
- It costs a few minutes per decision. Records that grow past one page are a signal the
  decision should have been split.
