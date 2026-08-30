# Task 09 — Agent workspace: tasks/reminders, quick replies, team notes

## Traceability
Epic:   docs/requirements/epics/EPIC-04-agent-workspace.md
Stories: US-141, US-142, US-143 (filed)
FEAT:   FEAT-28 (add row when filing — not yet in delivery-plan)
Source: docs/assessment/brief.md §4 Agent Dashboard.

## Work
1. `TicketTask` entity + EF config + migration `AddTicketTasksAndNotes`; CRUD handlers for
   create/toggle/update on ticket detail.
2. `QuickReply` entity + EF config + `QuickReplySeeder` (8 seeded replies) + startup seed call.
3. `TicketNote` entity + EF config; internal notes with `IsInternal` flag — excluded from portal API surface.

## Gate
- [x] Three entities in Domain layer, three EF configs, DbSets registered.
- [x] `QuickReplySeeder` registered in DI and called on startup.
- [x] Migration `AddTicketTasksAndNotes` generated.
- [x] Backend build clean (`dotnet build CustomerSupport.slnx` succeeded 0 errors).
