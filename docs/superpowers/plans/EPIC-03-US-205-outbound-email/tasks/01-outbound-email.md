# Task 01 — Outbound Email Reply

**Story/AC:** US-205, original AC-3.3 / AC-205.1..3
**Layer:** Backend Application/API, with US-203 sender
**Status:** not started

## Executable checklist

- [ ] Inspect `Ticket`, `TicketMessage`, record-message handler/validator, `IUserContext`,
  `TicketsController`, and the US-203 sender contract.
- [ ] First add failing unit tests in `Unit/Features/Tickets/SendTicketReplyCommandHandlerTests.cs`:
  `ValidReply_SendsEmailThenRecordsOutboundMessage`, `SubjectAlwaysContainsTicketReference`,
  `ProviderFailure_DoesNotRecordMessage`, `UnauthorizedAgent_DoesNotCallSender`,
  `EmptyBody_DoesNotCallSender`, and `SenderIdComesFromUserContext_NotRequest`.
- [ ] Add command/request/result/validator/handler in `Features/Tickets/Commands/SendTicketReply`.
- [ ] Compose `[TKT-nnnnnn]` server-side, call `IEmailSender`, then create/save one outbound Email
  `TicketMessage`; ensure all failure paths leave zero rows.
- [ ] Add failing integration methods `AC205_ValidReply_Returns201AndCreatesOutboundMessage`,
  `AC205_SubjectContainsTicketReference`, `AC205_ProviderFailure_ReturnsSafeFailureAndCreatesNoMessage`,
  and `AC205_AgentWithoutPermission_Returns403`.
- [ ] Wire `POST /api/Tickets/{id}/replies` and standard response metadata; do not alter manual
  `/messages` behavior.
- [ ] Run targeted tests, full backend tests/build, and paste actual output. Record send/DB atomicity
  limitations before changing status.

## Exact files

- New command files: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/SendTicketReply/`.
- New tests: `backend/tests/CustomerSupport.Tests/Unit/Features/Tickets/SendTicketReplyCommandHandlerTests.cs`
  and `Integration/SendTicketReplyEndpointTests.cs`.
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`.
- Inspect/reuse: existing ticket/message handlers, `IUserContext`, `IUnitOfWork`, and US-203
  `IEmailSender`; common `ticket.api.ts` only if a UI consumer is approved.

## Verification commands

```powershell
cd backend
dotnet test CustomerSupport.slnx --filter FullyQualifiedName~SendTicketReply
dotnet test CustomerSupport.slnx
dotnet build CustomerSupport.slnx
```

## Status evidence

Record provider calls, database row count before/after success/failure, exact subject, response code,
test counts, and sanitized output. No commands have been run while writing this plan.

## Deviation record

`None yet.` Record duplicate-request/outbox handling and any deliberate frontend deferral.
