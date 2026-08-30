# Task 6 — No response carries a credential

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../../../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) |
| Feature | `FEAT-02` Authentication and session |
| Criteria | AC-5 |
| Status | `done` |
| Commit | `cdee3a2` |

## Files

- `tests/CustomerSupport.Api.IntegrationTests/Authentication/CredentialHygieneTests.cs`

## Test evidence

4 tests pass. Full suite: 240 passing, 0 failing.

Run and observed, not assumed. See the commit for the pasted suite output.

## Deviations from the plan

None.

## The point of this task

The validation-path test is the one worth having. Frameworks routinely quote the offending value in a field error, so a password leaks on the path nobody inspects.
