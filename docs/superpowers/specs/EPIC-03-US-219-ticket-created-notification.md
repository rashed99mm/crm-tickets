# Ticket-created in-app notification via domain events

**Epic:** `EPIC-03 Communication Channels`
**Sprint:** `9 — Notification Gateway and Communication Channels`
**Feature:** `FEAT-15` (notification gateway slice touching the ticket workflow)
**Related stories:** `US-404` (portal submit ticket), `US-212`/`AC-29`..`AC-31` (ticket creation)
**Date:** 2026-08-28

## Problem

When a staff member creates a ticket in the admin-app, no in-app notification appears for anyone —
either in the UI or over the live SignalR channel. The vault of SignalR infrastructure is sound: the
hub is mapped on the Internal API (`WebApplicationExtensions.cs` → `/hubs/main`,
`RequireAuthorization("Authenticated")`), the admin-app proxy forwards `/hubs` to the Internal API
(`proxy.conf.json`), the frontend supplies a real `hubUrl` (`admin-app/app.config.ts:16`), and an
authenticated SignalR client demonstrably connects and joins `user:{userId}` (`MainHub.cs`,
`RealTimeNotifier.cs:24`). A live `@microsoft/signalr` client connected to `/hubs/main` on the
Internal API and subscribed to `NotificationReceived` successfully.

So the failure is not in the transport. It is upstream:

1. **Creating a ticket never produces a notification.** `CreateTicketCommandHandler` (Application,
   `Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs:26-69`) validates, creates
   the ticket, applies SLA targets, persists, and returns. It never calls the notification gateway.
   There is no domain-event consumer anywhere: `BaseEntity.AddDomainEvent` exists and `Ticket.Create`
   already raises `TicketCreatedEvent` (`Entities/Tickets/Ticket.cs:140`), but **no dispatcher
   publishes collected domain events** — `UnitOfWork.SaveChangesAsync` and
   `AppDbContext.SaveChangesAsync` (`Persistence/AppDbContext.cs:54`) save and return, and nothing
   consumes the raised events.

2. **The one background path that could catch up is a stub.** `Jobs/NotificationSender.cs:76-79`
   returns `Task.CompletedTask` and marks every polled `Pending` row `Sent` as if it had been pushed.

The user cannot rely on "fire a ticket and get told about it", which is a core expectation of a
ticketing CRM. This slice wires ticket creation to an in-app notification **through the domain-event
pipeline** — the mechanism the entities already raise events for but nothing listens to.

## Assumptions

- **A1 (recipients):** a ticket-created in-app notification is sent to **two recipients**:
  1. the **ticket's customer**, resolved to a login identity through the `AspNetUsers.CustomerId` ↔
     `Customer` link (migration `20260828094703_AddCustomerIdToAspNetUsers`,
     `ApplicationUser.CustomerId`); and
  2. the **acting staff member who created the ticket**, the `ActorId` the ticket already records as
     `CreatedBy` (`Ticket.CreatedBy`, and the same value carried on `TicketCreatedEvent.ActorId`).
  
  Each recipient is dispatched independently — if the customer has no linked portal user (typical
  for a staff-created record), the **creator is still notified** (AC-N7) and the customer leg is
  skipped, matching the original AC-N5. The customer's portal client (portal-app → External API hub,
  `proxy.portal.conf.json` → `:5095`) receives the `user:{customerUserId}` push; the creator's
  admin-app client receives the `user:{creatorUserId}` push.
- **A2:** the existing notification-gateway path is the delivery mechanism, exactly as the approved
  `EPIC-03-US-219-notification-gateway.md` (NG-5/NG-10) and the working `SlaBreachScanner` usage
  (`Jobs/SlaBreachScanner.cs:156-165`) specify: build a `NotificationDispatchRequest` with
  `Channels: [NotificationChannel.InApp]`, let `InAppNotificationChannelSender` persist the durable
  `Notification` row *before* the SignalR push, and let `RealTimeNotifier` push to `user:{recipient}`.
- **A3:** the domain-event dispatcher is new. It does not exist today and must, per the layered
  architecture, be driven from `AppDbContext.SaveChangesAsync` *after* a successful save, in a fresh
  dependency scope, so a handler's own `SaveChanges` does not re-enter the in-flight change tracker.
- **A4:** Only `TicketCreatedEvent` is handled in this slice. Other events already raised
  (`TicketStatusChangedEvent`, `TicketAssignedEvent`, `NotificationSentEvent`, `ContentPublishedEvent`)
  remain unhandled — they get new handlers only if/when a spec asks for them.

## Out of scope

- Notifying the whole staff queue when a customer submits a portal ticket. The event carries no
  staff-user list and the design for "fan out to role members" is not specified.
- Fixing `Jobs/NotificationSender.cs` beyond stopping it from double-marking. This slice does **not**
  route that background poller through the gateway (the gateway's InApp sender creates its *own* new
  row, so a generic "send this existing row" implementation would duplicate rows — see
  `InAppNotificationChannelSender.cs:51-52`). The poller is a separate inherited defect, recorded in
  the plan's gaps.
- Email and SMS channels; only the in-app channel is wired here.
- Any new API endpoint or frontend change (the admin UI already displays in-app notifications and
  listens on `NotificationReceived`).

## Acceptance criteria

- **AC-N1.** Given a `TicketCreatedEvent` for a ticket whose `CustomerId` has a linked portal user,
  when the event is dispatched, then the notification gateway is called for **both** recipients — the
  linked customer's user id and the ticket's actor (creator) user id — each with a single in-app
  channel, a `TICKET_CREATED` template code whose variables include the ticket reference, and a
  distinct deduplication key per recipient.
- **AC-N2.** Given a successful in-app dispatch, when the durable `Notification` row is written, then
  it targets the recipient's `UserId` (the customer's linked user or the creator), channel `InApp`,
  notification type `TICKET_CREATED`, and its message mentions the ticket reference. One row per
  recipient.
- **AC-N3.** Given an authenticated SignalR client connected in group `user:{recipientUserId}` (the
  customer's linked user **or** the creator), when an in-app notification is dispatched for that user,
  then the client receives a `NotificationReceived` payload (the gateway → in-app sender →
  `RealTimeNotifier` path already under test, asserted here end-to-end through the API).
- **AC-N4.** Given any `SaveChangesAsync` over a unit of work that saves created/modified aggregates,
  when domain events were raised on those aggregates, then each event is dispatched at most once and
  cleared after the save, and a handler failure neither fails the already-committed save nor crashes
  the request (events dispatch outside the save transaction).
- **AC-N5.** Given a `TicketCreatedEvent` for a customer with **no** linked portal user (or an empty
  `CustomerId`), when the handler runs, then the **customer leg** is skipped without throwing, and the
  **creator leg still dispatches** a notification to the actor (AC-N7).
- **AC-N6.** Given a `SaveChangesAsync` where no entity raised a domain event, when the save commits,
  then no dispatch occurs and no scope is opened.
- **AC-N7 (creator notified).** Given a `TicketCreatedEvent` whose `ActorId` identifies a real staff
  user, when the event is dispatched, then the notification gateway is called once with
  `RecipientUserId` = the creator's user id, channel `InApp`, `TICKET_CREATED` template with the ticket
  reference, and a deduplication key distinct from the customer's. This holds whether or not the
  customer has a linked user.

## Design

### Layering (dependency rule preserved)

| Artifact | Layer | Dependency |
|---|---|---|
| `IDomainEventHandler<TEvent>` | Application | Domain |
| `DomainEventDispatcher` (runtime) | Application | Domain + resolves handlers from `IServiceProvider` |
| `TicketCreatedEventHandler` | Application | `IIdentityUserService` (customer→user lookup), `INotificationGateway`, `ILogger` |
| `IIdentityUserService.FindByCustomerIdAsync` | Application port | implemented in Infrastructure |
| Dispatch hook in `SaveChangesAsync` | Infrastructure | `IDomainEventHandler<>` |
| `IDomainEventHandler<>` registration | Infrastructure | scans Application assembly |

The dispatcher lives in **Application** and consumes `IServiceProvider` (the same DI abstraction
`SlaBreachScanner`, `ResponseValidationBehavior` and the `AuditBehavior` pipeline already use). It
resolves every registered `IDomainEventHandler<TEvent>` for the raised `TEvent` and invokes them in
sequence. **Domain still references nothing.**

### Dispatch timing

`AppDbContext.SaveChangesAsync` (`Persistence/AppDbContext.cs:54`):

1. run the existing history guard and audit pass (unchanged),
2. `await base.SaveChangesAsync(ct)` — the committed save,
3. then, **after** the save returns, collect every distinct `IDomainEvent` from
   `ChangeTracker.Entries<BaseEntity>()` and clear them on the entities (clearing is what makes
   re-dispatch impossible, `AC-N4`),
4. dispatch the collected events through `IDomainEventDispatcher` using a **new `IServiceScope`**
   created from a `IServiceScopeFactory` (injected), so the handler's own
   `INotificationGateway.SendAsync` runs on its own `AppDbContext` and its own transaction and never
   touches the completing change tracker.

Handler failures are logged and swallowed (they must not turn a 200 ticket-create into a 500);
`AC-N4` asserts this.

### The handler

`TicketCreatedEventHandler` (Application, `Features/Tickets/Events/`) first resolves the recipient,
then dispatches only when a portal user is linked to the customer (`AC-N1`/`AC-N5`):

```csharp
var customerUserId = await _users.FindByCustomerIdAsync(@event.CustomerId, ct);
if (customerUserId is null || customerUserId == Guid.Empty)
    return;                                  // AC-N5: not a portal user, notify nobody

await _gateway.SendAsync(new NotificationDispatchRequest(
    TemplateCode: "TICKET_CREATED",
    RecipientUserId: customerUserId,
    Channels: [NotificationChannel.InApp],
    Variables: new() { ["Title"] = "Ticket created",
                      ["Message"] = $"Ticket {ticket.Reference} has been created." },
    Email: null, PhoneNumber: null, BypassUserSettings: true,
    DeduplicationKey: $"ticket-created:{@event.TicketId}", CorrelationId: @event.TicketId.ToString()), ct));
```

`IIdentityUserService.FindByCustomerIdAsync(Guid customerId)` is a new Application port that
`IdentityUserService` implements by querying `AppDbContext.Users` on `CustomerId`. The renderer
(`NotificationTemplateRenderer.cs`) turns `Variables["Title"]`/`Variables["Message"]` into the
rendered notification; `InAppNotificationChannelSender` persists the durable `Notification` row
(Channel=InApp) **before** the SignalR push and marks it `Sent` on success (NG-5/NG-10), which is
exactly `AC-N2`/`AC-N3`.

## API and error contract

No new HTTP endpoint. The only new surface is the `IIdentityUserService.FindByCustomerIdAsync`
port (a query, `Task<ApplicationUser?>`). Existing envelope, `INotificationGateway`,
`NotificationDispatchResult` and `ApplicationErrors.Notification.*` contract. `AC-N5` guarantees a
customer with no linked portal user sends nothing rather than producing an
`INAPP_REQUIRES_USER` failure at the sender.
