# FEAT-15 ticket-created in-app notification via domain events — Implementation Plan

**Spec:** `docs/superpowers/specs/EPIC-02-US-016-ticket-created-notification-design.md` (approved 2026-08-28)
**Sprint:** `9 — Notification Gateway and Communication Channels`
**Status:** pending

## Goal

Wire ticket creation to an in-app notification delivered through the existing notification gateway,
dispatching the already-raised `TicketCreatedEvent` via a newly added domain-event dispatcher. The
recipient is the ticket's customer resolved to its linked portal user (spec A1).

## What already exists (do not reinvent)

- `Ticket.Create` raises `TicketCreatedEvent(TicketId, Reference, CustomerId, ActorId)` at
  `Domain/Entities/Tickets/Ticket.cs:140`, collected by `BaseEntity.AddDomainEvent`
  (`Domain/Entities/BaseEntity.cs:19`) into `DomainEvents`.
- **No dispatcher consumes these events today** — `UnitOfWork.SaveChangesAsync`
  (`Infrastructure/Persistence/UnitOfWork.cs:17`) and `AppDbContext.SaveChangesAsync`
  (`Infrastructure/Persistence/AppDbContext.cs:54`) save and return. This is the gap.
- The delivery mechanism is the approved gateway: `INotificationGateway.SendAsync`
  (`Application/Notifications/Contracts.cs:40`). The working reference is `SlaBreachScanner`
  (`Infrastructure/Jobs/SlaBreachScanner.cs:156-165`), which builds a `NotificationDispatchRequest`
  with `Channels: [NotificationChannel.InApp]` and `Variables["Title"]`/`["Message"]`.
  `InAppNotificationChannelSender` (`Infrastructure/Notifications/InAppNotificationChannelSender.cs:44-64`)
  persists the durable row *before* the SignalR push and marks it `Sent` on success (NG-5/NG-10).
- `TicketCreatedEvent.CustomerId` resolves to a login user through `ApplicationUser.CustomerId`
  (`Domain/Entities/Identity/ApplicationUser.cs:23`), populated only by portal registration
  (migration `20260828094703_AddCustomerIdToAspNetUsers`).

## Dependency rule

| Kind | Home | Test |
|---|---|---|
| `IDomainEventHandler<TEvent>`, `IDomainEventDispatcher` | Application | Unit |
| `TicketCreatedEventHandler` | Application | Unit + integration |
| `IIdentityUserService.FindByCustomerIdAsync` port | Application | — (interface) |
| `IdentityUserService.FindByCustomerIdAsync` impl | Infrastructure | Integration |
| Dispatch hook in `AppDbContext.SaveChangesAsync` | Infrastructure | Integration |

`Domain` still references nothing. `Application` references only `Domain` (plus the BCL
`IServiceProvider`/`ILogger` DI abstractions, as `ResponseValidationBehavior` already does).
`Infrastructure` references `Application` for the interfaces it implements. Api.Shared is untouched.

## Contract fragments (grounded in the files they will live in)

```csharp
// Application/Events/IDomainEventHandler.cs  (new)
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct = default);
}

// Application/Interfaces/IIdentityUserService.cs  (add one method)
Task<ApplicationUser?> FindByCustomerIdAsync(Guid customerId, CancellationToken ct = default);

// Infrastructure/Services/IdentityUserService.cs  (add one method)
public Task<ApplicationUser?> FindByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
    => _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.CustomerId == customerId, ct);

// Application/Features/Tickets/Events/TicketCreatedEventHandler.cs  (new)
public sealed class TicketCreatedEventHandler(IIdentityUserService users,
    INotificationGateway gateway, ILogger<TicketCreatedEventHandler> logger)
    : IDomainEventHandler<TicketCreatedEvent>
{
    public async Task Handle(TicketCreatedEvent e, CancellationToken ct)
    {
        var userId = await users.FindByCustomerIdAsync(e.CustomerId, ct);   // AC-N1/AC-N5
        if (userId is null)
        {
            logger.LogInformation("No portal user linked to customer {CustomerId}; skipping ticket-created notification", e.CustomerId);
            return;
        }

        await gateway.SendAsync(new NotificationDispatchRequest(
            TemplateCode: "TICKET_CREATED",
            RecipientUserId: userId,
            Channels: [NotificationChannel.InApp],
            Variables: new Dictionary<string, string>
            {
                ["Title"] = "Ticket created",
                ["Message"] = $"Ticket {e.Reference} has been created."
            },
            Email: null, PhoneNumber: null, BypassUserSettings: true,
            DeduplicationKey: $"ticket-created:{e.TicketId}",
            CorrelationId: e.TicketId.ToString()), ct);                       // AC-N1
        // AC-N2/AC-N3 are produced by InAppNotificationChannelSender + RealTimeNotifier.
    }
}
```

## Tasks

- [Task 01](tasks/task-01-lookup-port.md) — `IIdentityUserService.FindByCustomerIdAsync` (+ impl). Covers `AC-N1`, `AC-N5`.
- [Task 02](tasks/task-02-domain-event-dispatcher.md) — `IDomainEventHandler<T>`, `IDomainEventDispatcher`,
  dispatcher runtime, DI registration, `SaveChangesAsync` hook. Covers `AC-N4`, `AC-N6`.
- [Task 03](tasks/task-03-ticket-created-handler.md) — `TicketCreatedEventHandler` + unit test naming
  each AC. Covers `AC-N1`, `AC-N5`.
- [Task 04](tasks/task-04-evidence-gate.md) — integration tests (real LocalDB) for `AC-N2`/`AC-N3`,
  full suite, clean `--warnaserror` build, story-status update.

### Explicitly not in this plan (recorded, not forgotten)

- `Infrastructure/Jobs/NotificationSender.cs:76-79` — the no-op `SendAsync` stub stays untouched.
  Routing the poller through the gateway would make `InAppNotificationChannelSender` create a *second*
  durable row per dispatch (it always `AddAsync` a fresh `Notification`, `InAppNotificationChannelSender.cs:51`),
  so it is a separate inherited defect, tracked in the delivery plan.
- Notifying the acting staff member, or fanning out to a staff role group — out of scope per spec A1.
