# EPIC-09 · Security & administration

| | |
|---|---|
| **Epic** | `EPIC-09` |
| **Priority** | P0 (S1 share) |
| **Stories** | 4 specified · 216-point plan share: 18 pts |
| **Sprints** | 1 |

## Goal

Secure the system and provide administrative configuration *(rule specification §8)*. S1 delivers
the security half: authentication that discloses nothing on failure, a role boundary with real
refusals, and certainty that credentials are never emitted.

## Why this epic exists

Authentication is where "works" and "safe" diverge. Three refusals define it here: a failed sign-in
reveals nothing — wrong password and locked account are indistinguishable, because a distinct
lockout signal confirms the account exists (`BR-12`, `AC-67`); role boundaries refuse rather than
advise — an agent's token at a supervisor-only endpoint gets 403 (`AC-4`); and no endpoint, log line
or error response ever carries a password or hash (`AC-5`, `NFR-5`). Staff accounts are created
administratively — seeded in sprint 1 — because there is no public self-registration anywhere in
the product as described (`B3`).

## Stories

| Story | Title | Priority | Points | Status | Criteria |
|---|---|---|---|---|---|
| [US-112](../user-stories/US-112-staff-sign-in.md) | Staff sign in and receive a role-carrying credential | P0 | 8 | `not started` | AC-1, AC-3 |
| [US-113](../user-stories/US-113-failed-sign-in-reveals-nothing.md) | A failed sign-in reveals nothing, lockout included | P0 | 5 | `not started` | AC-2, AC-6, AC-67 |
| [US-114](../user-stories/US-114-role-permissions-refuse.md) | Role permissions refuse what the role may not do | P0 | 3 | `not started` | AC-4 |
| [US-115](../user-stories/US-115-credentials-never-emitted.md) | Credentials never appear anywhere | P0 | 2 | `not started` | AC-5 |

Absorbs former epic `EP-1.02` Identity and access.

## Reserved backlog (unspecified — titles only, no fabricated rules)

| Rule proposal | Future home | Blocked on |
|---|---|---|
| US-074 Manage Users · US-075 Manage Roles · US-076 Manage Permissions | slice S9 *(proposed)*, sprint 12 | `G-2` — the slice itself awaits a product decision; compliance exposure tracked as `RSK-8` |
| US-077 Configure Ticket Categories · US-078 Configure Ticket Priorities | S9; taxonomy must be agreed before launch regardless — `DEP-4` | `DEP-4`, `G-2` |
| US-079 View Audit Logs | S9 system-wide log; ticket-level audit already ships as US-121 | `RSK-8`, `OQ-6` |
| US-080 Configure System Settings | S9 | `G-2` |
