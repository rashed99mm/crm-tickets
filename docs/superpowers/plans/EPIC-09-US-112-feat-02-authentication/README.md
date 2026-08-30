# FEAT-02 — Authentication and session · execution record

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


Per-task records for [`implementation-plan/implementation-plan.md`](./implementation-plan.md),
kept for historical access: what each task actually delivered, its commit, the test evidence, and
every deviation from what the plan said to do.

**Why these exist.** A plan states intent; these state outcome. The difference between the two is
where the real engineering happened, and it is the only durable record of *why* the code looks the
way it does rather than the way it was planned. Six of the eight tasks below deviated from the plan,
each for a reason found by running it.

| Task | Title | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-token-issuer.md) | Mint access tokens behind a port | part of AC-1 | `bbe70bc` | `done` |
| [02](./tasks/task-02-identity-and-sign-in.md) | Identity registration, lockout and the sign-in use case | AC-1, AC-2, AC-6, AC-67 | `bdc48d3` | `done` |
| [03](./tasks/task-03-token-validation.md) | Validate tokens and define role policies | AC-3, part of AC-4 | `9dbbc79` | `done` |
| [04](./tasks/task-04-sign-in-endpoint.md) | `POST /api/auth/sign-in` | AC-1, AC-2, AC-6, AC-67 | `4257f7a` | `done` |
| [05](./tasks/task-05-protected-endpoints.md) | Refuse bad tokens, enforce roles | AC-3, part of AC-4 | `61eee59` | `done` |
| [06](./tasks/task-06-credential-hygiene.md) | No response carries a credential | AC-5 | `cdee3a2` | `done` |

Plan tasks 2 and 3 were committed together, as the plan instructed, because task 2 alone leaves a
red build. Plan task 8 (updating story statuses and traceability) is recorded in the documentation
commit rather than here.

## Criteria delivered

| Criterion | Status | Proven by |
|---|---|---|
| `AC-1` token with id and role claims | `done` | `JwtTokenIssuerTests`, `SignInHandlerTests`, `Valid_Credentials_Return_200_With_A_Token_And_Role_Claims` |
| `AC-2` 401 without disclosing existence | `done` | `Unknown_Email_Is_Byte_Identical_To_A_Wrong_Password` |
| `AC-3` 401 for a missing or bad token | `done` | four `ProtectedEndpointTests` cases |
| `AC-4` 403 for an agent on a supervisor-only endpoint | **`partial`** | policy proven against guarded probes; a shipped supervisor-only endpoint arrives with `US-117` in sprint 2 |
| `AC-5` no credential in any response | `done` | `CredentialHygieneTests` ×4 |
| `AC-6` lockout after a threshold | `done` | `Repeated_Failures_Lock_The_Account_And_The_Refusal_Looks_The_Same` |
| `AC-67` lockout identical to a wrong password | `done` | the same test, plus `Lockout_Is_Indistinguishable_From_A_Wrong_Password` |

## Gaps accepted, not hidden

1. **`AC-4` is partial.** The policy works; no shipped endpoint requires it yet. Closing it needs
   `US-117`.
2. **No expired-token test.** It needs a controllable clock inside the running host or a real wait.
   `ValidateLifetime` is on with an explicit 30-second skew.
3. **No refresh flow and no sign-out endpoint.** `DomainKey.LogoutSuccess` exists in the catalogue
   but no S1 criterion asks for the endpoint.

## What is not done for FEAT-02

**The frontend half.** `US-125` — the sign-in screen — has not been built, so **`FEAT-02` has not
shipped** under this project's own definition: a feature ships as backend plus frontend plus tests,
together. The next artifact is the frontend plan for this same feature, not sprint 2's backend.
