# Task 6 — Cross-cutting security and evidence gate

**Status:** not started
**Criteria:** `CC-27`, `CC-28`, `CC-29`, plus re-verification of `CC-1`..`CC-26` together
**Plan section:** [`implementation-plan.md#task-6--cross-cutting-security-and-evidence-gate`](../implementation-plan.md#task-6--cross-cutting-security-and-evidence-gate)
**Depends on:** Tasks 1–5

## Scope

The full-suite run, the build-under-warnings-as-errors check, and the manual signature/secret/
client-supplied-id audit across every new controller and hub. This is the task whose output is
allowed to be pasted as evidence that `FEAT-24`..`FEAT-27` shipped — no earlier task's output
substitutes for it.

## When executed, record here

- Full build output (`dotnet build backend/CustomerSupport.slnx --warnaserror`).
- Full focused test output.
- The audit findings from plan steps 1–3, including anything that had to be fixed as a result.
