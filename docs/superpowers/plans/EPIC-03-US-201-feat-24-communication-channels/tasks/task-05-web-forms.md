# Task 5 — Web forms

**Status:** not started
**Criteria:** `CC-20`, `CC-21`, `CC-22`, `CC-23`
**Plan section:** [`implementation-plan.md#task-5--web-forms`](../implementation-plan.md#task-5--web-forms)
**Depends on:** Task 1

## Scope

`SubmitWebFormTicketCommand`, `WebFormRateLimiter`, `WebFormController` (anonymous, `ExternalApi`).
Before starting, check whether `FEAT-22`/`US-404` (customer portal ticket submission) has landed —
if so, re-scope this task to add only the anonymous entry point on the existing application-layer
command rather than duplicating it (spec `A4`).

## When executed, record here

- Commit hash.
- Test command run and its actual output.
- Whether `FEAT-22` had landed first, and if so, what was reused vs. duplicated.
- Any deviation from the plan section above, and why.
