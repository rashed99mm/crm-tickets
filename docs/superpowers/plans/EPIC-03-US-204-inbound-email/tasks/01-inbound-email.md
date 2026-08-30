# Task 01 — Inbound Email

**Story/AC:** US-204, original AC-3.2 / AC-204.1..4
**Layer:** Backend application, persistence, and signed webhook
**Status:** not started

## Executable checklist

- [ ] Inspect `Ticket`, `TicketMessage`, `IUnitOfWork`, `AppDbContext`, customer/ticket handlers, and
  US-205 reference convention before selecting key types or sender identity.
- [ ] First add unit tests in `Unit/Email/InboundEmailProcessorTests.cs` named
  `VerifiedEmailWithoutReference_CreatesTicketAndInitialMessage`,
  `VerifiedEmailWithReference_AppendsToTicket`, `DuplicateProviderMessageId_IsIdempotent`,
  `InvalidSignature_IsRejectedBeforeParsing`, `TransientPersistenceFailure_IsRetried`, and
  `PermanentFailure_CreatesDeadLetterWithoutPartialWrite`.
- [ ] Add `InboundEmail`, processor port, command/handler, reference parser, and ingestion-log entity.
- [ ] Add failing integration tests in `Integration/InboundEmailEndpointTests.cs` named
  `VerifiedWebhook_CreatesTicketAndMessage`, `VerifiedWebhook_AppendsToReferencedTicket`,
  `DuplicateWebhook_DoesNotDuplicateRows`, `InvalidSignature_WritesNothing`, and
  `MalformedPayload_ReturnsValidationEnvelope`.
- [ ] Add EF configuration/DbSet/migration with unique `ExternalMessageId`; use one transaction for
  ingestion log, ticket, and message writes.
- [ ] Add raw-body signature verification and route wiring; invalid signatures must stop before parse.
- [ ] Run targeted tests, inspect database row counts and sanitized logs, then run full test/build
  commands and paste actual output.

## Exact files

- New Application: `Communication/IInboundEmailProcessor.cs`, `Communication/InboundEmail.cs`,
  `Features/Email/ProcessInboundEmail/{ProcessInboundEmailCommand,ProcessInboundEmailCommandHandler,InboundEmailReferenceParser}.cs`.
- New Domain/Infrastructure: `Entities/Email/EmailIngestionLog.cs`,
  `Persistence/Configurations/EmailIngestionLogConfiguration.cs`.
- New API/test: `InternalApi/Controllers/InboundEmailController.cs`,
  `Integration/InboundEmailEndpointTests.cs`, `Unit/Email/InboundEmailProcessorTests.cs`.
- Modify: `AppDbContext.cs`, `UnitOfWork.cs` only if needed, `ServiceCollectionExtensions.cs`, and
  `InternalApi/Program.cs`.

## Verification commands

```powershell
cd backend
dotnet test CustomerSupport.slnx --filter FullyQualifiedName~InboundEmail
dotnet test CustomerSupport.slnx
dotnet build CustomerSupport.slnx
```

## Status evidence

Record migration name, signature result, ticket/message/log counts for create/append/duplicate,
dead-letter status, exact test counts, and sanitized command output. No commands have been run while
writing this plan.

## Deviation record

`None yet.` Record provider webhook format, sender resolution, retry/dead-letter mechanism, or key
type incompatibility explicitly.
