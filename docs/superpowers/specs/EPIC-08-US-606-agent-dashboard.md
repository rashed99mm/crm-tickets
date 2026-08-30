# Agent dashboard

**Date:** 2026-08-26
**Story:** `MVP-12` in [`../../requirements/mvp/epic-4-agent-workspace.md`](../../requirements/mvp/epic-4-agent-workspace.md)
**Brief area:** 4 — Agent Dashboard

## Problem

An agent signing in lands on the whole ticket queue and has to filter it every morning to find their
own work. Nothing tells them the **shape** of their workload — whether four things need starting or
twelve are waiting on a customer.

A supervisor has the same problem in reverse: nothing shows how much work is sitting unassigned.

## Assumptions

- **A15.** "Assigned tickets" means tickets currently assigned to the signed-in agent. Watched or
  followed tickets are not a concept in this product.
- **A16.** Counts are live per request. No caching, no refresh timer — a support tool that shows
  stale counts is worse than one that shows none.
- **A17.** `Resolved` and `Closed` are not "my open work". The dashboard counts the current
  non-terminal workflow statuses: `New`, `Open`, `Assigned`, `In Progress`, `Waiting for Customer`,
  and `Waiting for Internal Team`.

## Out of scope

Charts · date ranges · a team view · tasks and reminders · quick replies · SLA countdowns ·
notifications. Four of brief area 4's five bullets are deliberately out — see the epic for why.

## Acceptance criteria

Appended; nothing renumbered.

- **AC-77** (P0) Given I sign in, when I open the dashboard, then I see my open tickets — newest
  first — without setting any filter.
- **AC-78** (P0) Given my tickets, then I see a count for each current non-terminal status, so I
  can read my workload's shape rather than counting rows.
- **AC-79** (P0) Given a ticket on the dashboard, when I click it, then I open that ticket's detail.
- **AC-80** (P1) Given I have nothing assigned, then the dashboard says so plainly and is **visually
  distinct from a failure**.
- **AC-81** (P1) Given the request fails, then I see an error with a retry, distinct from having no
  work.
- **AC-82** (P1) Given I am a supervisor, then I additionally see **how many tickets are
  unassigned**, and clicking that count opens the queue filtered to them.

## Design

### No new endpoint — a deliberate constraint on this story

`GET /api/Tickets` already supports `mine=true`, `status=`, `assigneeId=` and returns a paged
envelope carrying `totalCount`. The dashboard is composed from it:

| Panel | Request |
|---|---|
| My open list | `?mine=true&pageSize=10` |
| Count per status | `?mine=true&status=New&pageSize=1` → read `totalCount` (×3) |
| Unassigned count (supervisor) | `?assigneeId=&pageSize=1` … **see the open question** |

Reading `totalCount` from a `pageSize=1` request is a deliberate trade: four small round trips
against building a purpose-made aggregate endpoint. For a single agent's queue that is cheap and it
keeps this story to one layer.

**If it turns out an aggregate endpoint is needed, that is a finding to record — not an assumption to
build on.** Measure first.

### Open question, to resolve during implementation

There is currently **no way to ask for unassigned tickets**. `assigneeId` is a `Guid?` filter; a
missing value means "no filter", not "assignee is null". `AC-82` therefore needs one of:

1. a `unassigned=true` flag on `GetTicketsQuery`, or
2. treating `assigneeId=<empty guid>` as "is null" — rejected, that is a magic value.

**Option 1, and it is a real backend change** — small, but it means this story is not purely
frontend. Recorded here rather than discovered mid-build.

### Screen

`/dashboard`, inside the guarded shell, becomes the post-sign-in landing route in place of
`/tickets`. The shell gains a "Dashboard" nav item.

`AsyncState` per panel, so a failing count does not blank the list.

## Testing

| Level | Covers |
|---|---|
| Integration | `unassigned=true` returns only tickets with no assignee, and combines with other filters |
| Component | `AC-77`…`AC-82`, including the supervisor-only panel being absent for an agent |

`AC-80` and `AC-81` reuse the queue's pattern: the empty state carries **no retry** and the error
state does, which is both the honest signal and the visual difference.
