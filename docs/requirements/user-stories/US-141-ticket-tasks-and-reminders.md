# US-141 · Ticket tasks and reminders

| Field | Value |
|---|---|
| **Story** | `US-141` |
| **Epic** | [EPIC-04 Agent workspace](../epics/EPIC-04-agent-workspace.md) |
| **Feature** | `FEAT-28` Agent workspace tasks |
| **Layer** | Both |
| **Actor** | Support Agent |
| **Priority** | P0 |
| **Estimate** | 3 points |
| **Status** | `done` |

## Story

**As a support agent**, **I want** to create tasks with due dates on tickets, **so that** I can track follow-up work and not miss deadlines.

## Acceptance criteria

#### AC-141.1 — Create task on ticket

Given a ticket detail view, when I enter a title and optional due date and save, then a task is attached to that ticket and visible to any agent who opens it.

#### AC-141.2 — Task list on ticket

Given a ticket with tasks, when I open the ticket, then I see all its tasks listed with title, due date, and done/not-done state.

#### AC-141.3 — Mark task done

Given a task on a ticket, when I toggle its done state, then the task updates immediately and the overdue flag recalculates.

#### AC-141.4 — Overdue tasks render red

Given a task with a past due date that is not done, when it is displayed anywhere, then it renders in a red visual style distinct from non-overdue tasks.

#### AC-141.5 — Agent sees own open tasks on dashboard

Given an agent, when I open my assigned-ticket view, then I see a count of my open tasks across all my tickets, and clicking opens the task list.

## SQL tables

```sql
TicketTasks(Id, TicketId, Title, DueAt, IsDone, CreatedBy, CreatedAt)
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then |
|---|---|---|---|---|
| TC-01 | AC-141.1 | Integration | `CreateTask_PersistsAndAssociatesWithTicket` | Given a ticket, when an agent creates a task, then the task is in the DB linked to that ticket |
| TC-02 | AC-141.2 | Integration | `GetTasksByTicketId_ReturnsAll` | Given a ticket with 3 tasks, when queried by ticketId, then all 3 are returned |
| TC-03 | AC-141.3 | Integration | `ToggleTaskDone_UpdatesState` | Given a task, when IsDone is toggled, then the new value is persisted |
| TC-04 | AC-141.4 | Component | `OverdueTask_RendersRedStyle` | Given an overdue undone task, when rendered, then it has a red visual style |
| TC-05 | AC-141.5 | Component | `AgentDashboard_ShowsOpenTaskCount` | Given an agent with open tasks, when dashboard loads, then a task count is displayed |

## Open questions

None.

## Status evidence

Backend: `TicketTask` entity + EF config + migration `AddTicketTasks`. CRUD handlers.
Frontend: task list component on ticket detail + overdue red styling + dashboard task count.
