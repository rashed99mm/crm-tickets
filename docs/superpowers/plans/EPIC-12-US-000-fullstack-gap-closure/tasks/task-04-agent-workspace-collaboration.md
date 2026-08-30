# Task 04 - Agent Workspace Collaboration

**Status:** Ready  
**Closes gaps:** `/agent-workspace`, tasks/reminders, quick replies, collaboration/internal chat, presence.

## Files

- Backend domain: `TicketTask.cs`, `QuickReply.cs`, new internal thread/message entities if needed
- Backend API: new `AgentWorkspaceController.cs`, `QuickRepliesController.cs`, `PresenceController.cs`
- Realtime: `MainHub.cs`, `RealTimeNotifier.cs`
- Frontend API: `common/src/lib/agent-workspace`, `common/src/lib/support`
- Frontend UI: new `admin-app/src/app/features/agent-workspace`

## Implementation

- Add agent workspace route/nav item.
- Add task list with create/complete/reassign/reminder.
- Add quick reply manager and insert picker.
- Add private internal thread for staff collaboration.
- Add presence SignalR events and online agent rail.

## Code Example

```ts
export interface AgentWorkspaceDto {
  readonly assignedTickets: readonly TicketSummary[];
  readonly dueTasks: readonly TicketTaskDto[];
  readonly quickReplies: readonly QuickReplyDto[];
  readonly onlineAgents: readonly PresenceDto[];
}
```

```csharp
public sealed record CompleteTicketTaskCommand(Guid TaskId)
    : IRequest<Response<TicketTaskDto>>;
```

## Acceptance

- [ ] `/agent-workspace` is protected and visible in nav.
- [ ] Tasks and reminders are persisted and filterable.
- [ ] Quick replies can be searched and inserted into composers.
- [ ] Internal chat is staff-only.
- [ ] Presence is driven by SignalR, not hardcoded avatars.
- [ ] Stitch agent workspace review passes.

## Evidence

- Slice 01 moved the chat workspace AI rail from static local drafts to API-backed generated drafts and preserved active-composer insertion. Broader `/agent-workspace`, quick reply manager, staff-only internal chat, and SignalR presence remain open for this task.
- Slice 03 added escalation-owner handoff in the ticket detail action rail. Broader `/agent-workspace` remains open.
