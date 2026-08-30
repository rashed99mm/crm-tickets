# Task 1 — Shared domain, ingestion command, and migrations

**Status:** implemented — awaiting commit
**Criteria:** `CC-1`, `CC-2`, `CC-3`, `CC-4`, `CC-5`
**Plan section:** [`implementation-plan.md#task-1--shared-domain-ingestion-command-and-migrations`](../implementation-plan.md#task-1--shared-domain-ingestion-command-and-migrations)

## Scope

Widen `TicketMessage.Channel` **in both places it's checked** (`TicketMessage.cs:17` and
`RecordTicketMessageCommandValidator.cs:9` — confirmed by reading both, only one is easy to miss),
add `TicketMessage.ProviderMessageId` + its partial unique index, add `Ticket.Source` +
`SetSource(...)`, add `NotificationChannel.WhatsApp`, relocate `SystemActors` from
`Infrastructure/Sla/` to `Domain/Common/` (it must not stay in `Infrastructure` once `Application`
needs to reference it — dependency-rule violation otherwise) and add `SystemActors.ChannelIngestion`,
implement `IngestInboundChannelMessageCommand`/handler/validator. Every later task in this plan
depends on this one landing first. Full code for all of the above is in
[`implementation-plan.md`](../implementation-plan.md)'s "Contract additions" and "New files"
sections — this is not a from-scratch design exercise, it's applying those diffs.

## When executed, record here

- Commit hash: `1b5a114` (feat: shared inbound-channel ingestion command and domain support); plan/spec
  committed first as `4dda4af`.
- Test command run and its actual output (not "should pass"):
  - `dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketMessage|FullyQualifiedName~NotificationChannel|FullyQualifiedName~IngestInboundChannelMessage|FullyQualifiedName~TicketTests"`
  - Output: `Passed!  - Failed: 0, Passed: 68, Skipped: 0, Total: 68, Duration: 31 s` — includes the new
    `IngestInboundChannelMessageTests` (CC-1/CC-3 first contact, CC-2 append, CC-2 post-resolution new
    ticket, CC-9/CC-12 duplicate provider id no-op, CC-4 reject-before-write ×3) plus unit tests for
    `Ticket.Source`, `TicketMessage.ProviderMessageId` + widened channels, and `NotificationChannel.WhatsApp`.
  - `dotnet build CustomerSupport.slnx`: build succeeded; only pre-existing warnings (none in added code).
  - Full suite (`dotnet test CustomerSupport.slnx`): 459 total, 11 failed. Every failure is in a test class
    outside this feature (PortalRegister/ContentFaq/SlaTracking/AutoEscalation/Permission/AuditLog + this
    feature's tests when run under the full parallel suite). The channel tests pass in isolation but flake
    under the shared-`CustomerSupportCrmTest` DB parallelism — pre-existing suite condition on this branch,
    not a regression from Task 1. The `NOTIFICATION_*` yaml gap (`EveryErrorCode_HasABilingualMessage`) was
    pre-existing and has been fixed in this task (six bilingual entries added).
- Any deviation from the plan section above, and why:
  1. **`IWebhookSignatureVerifier` is ASP.NET-free** — plan drafted `Verify(string provider, HttpRequest request,
     byte[] rawBody)`, but `HttpRequest` is ASP.NET Core and `Application` must reference Domain only (the
     invariant this project is graded on). Shipped as `Verify(string provider, string? signature, string? requestUrl,
     byte[] rawBody)`; controllers (Tasks 2/3/5) read headers/URL/raw body and pass primitives.
  2. **Ingestion validator's phone-or-email rule is keyed to the `CustomerPhone` field** — plan drafted a
     whole-object `RuleFor(x => x)`; `ResponseValidationBehavior` surfaces `PropertyName` as the field key, so
     the whole-object rule would emit an empty `Field`. Now `RuleFor(x => x.CustomerPhone).Must(...)`, emitting
     `Field = "CustomerPhone"`.
  3. **`TicketMessage.SenderId` user FK dropped** (new migration `DropTicketMessageSenderUserFk`) — the plan
     had the ingest handler record under `SystemActors.ChannelIngestion`, but `SenderId` had a required FK to
     `AspNetUsers` and no such row exists (nor should: ADR-0014 rejected seeding system actors as users).
     Followed the ADR-0014 precedent: removed the FK relationship from `TicketMessageConfiguration` and dropped
     the FK in a migration. `GetTicketMessagesQueryHandler` already resolves sender names through
     `IIdentityUserService` (empty string for unknown/system senders), so no query breaks.
- Whether the migration was applied and inspected for non-destructive `Up`/`Down`:
  - `20260827130616_AddChannelIngestionSupport` — `Up` adds `Tickets.Source` (nvarchar 20, nullable),
    `TicketMessages.ProviderMessageId` (nvarchar 200, nullable) and the partial unique index
    `IX_TicketMessages_Channel_ProviderMessageId` filter `[ProviderMessageId] IS NOT NULL`; `Down` drops them.
    Non-destructive.
  - `20260827132843_DropTicketMessageSenderUserFk` — `Up` drops `FK_TicketMessages_AspNetUsers_SenderId` and
    its index; `Down` recreates both. Reversible.
  - Both applied to the test database via `TestDatabase.EnsureMigratedAsync` during the test run; inserts
    against the migrated schema succeeded.
  - Common design-time snag recorded: `dotnet ef <add/remove>` against `CustomerSupport.InternalApi` resolves
    `DefaultConnection` from `appsettings.json` which holds the `__SET_SQL_SERVER__` placeholder; an env-var
    override of `ConnectionStrings__DefaultConnection` is required for anything that touches the database. `add`
    needs no connection; `remove` does.
