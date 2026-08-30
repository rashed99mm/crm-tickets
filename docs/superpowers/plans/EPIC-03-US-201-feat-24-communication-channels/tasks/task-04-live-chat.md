# Task 4 — Live chat

**Status:** not started
**Criteria:** `CC-14`, `CC-15`, `CC-16`, `CC-17`, `CC-18`, `CC-19` (backend seams for `CC-25`/`CC-26`)
**Plan section:** [`implementation-plan.md#task-4--live-chat`](../implementation-plan.md#task-4--live-chat)
**Depends on:** Task 1 (ticket creation/append on conversion)

## Scope

`LiveChatSession`/`LiveChatMessage` entities, the five chat commands + waiting-queue query, the new
anonymous `ChatHub` (`/hubs/chat`, separate from `MainHub` per `A12`), customer-side and agent-side
controllers, the `Waiting → Abandoned` timeout job, and the two migrations.

## When executed, record here

- Commit hash.
- Test command run and its actual output.
- Confirmation `MainHub`'s mapping/policy was left unchanged (`A12`) — this is the detail most
  likely to regress `FEAT-15`'s in-app notification groups if done carelessly.
- Any deviation from the plan section above, and why.
