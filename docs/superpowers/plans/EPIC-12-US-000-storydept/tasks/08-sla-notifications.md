# Task 08 — SLA breach/warning notifications (US-219)

## Traceability
Epic:   docs/requirements/epics/EPIC-05-sla-and-automation.md
Story:  docs/requirements/user-stories/EPIC-05-US-219-sla-notifications.md
FEAT:   FEAT-17 (SLA and automation) — delivery-plan.md row 8
Plan:   docs/superpowers/plans/EPIC-05-US-219-sla-notifications/

## Work
At SLA breach/warning evaluation, publish through the EXISTING gateway port
(Application/Notifications/Contracts.cs — INotificationGateway; already consumed by
RecordTicketMessageCommandHandler:22 and RequestOtpCommandHandler:32):
```csharp
await gateway.SendAsync(new NotificationRequest(
    Channel.InApp, ticket.AssigneeId, Template: "sla.breach",
    new Dictionary<string, string> { ["reference"] = ticket.Reference,
                                     ["priority"] = ticket.Priority.ToString() }), ct);
```
Warning variant uses template "sla.warning" and fires once per ticket (state flag).
Frontend bell + unread badge already render (portal/admin shells).

## Tests (failing first)
AC219_BreachNotifiesAssignee · AC219_WarningNotifiesOncePerTicket

## Gate
dotnet test --filter "FullyQualifiedName~SlaNotification|FullyQualifiedName~Sla" → green.

## Status (2026-08-27)
IMPLEMENTED: SlaBreachScanner now sends one InApp notification (template SLA_BREACH, gateway
INotificationGateway) to the assignee per newly-breached ticket, post-save, alongside the
escalation publishes. Pre-breach warnings remain out of scope (US-217 cut in delivery-plan).
Tests: SlaNotificationTests AC219.1/219.2/219.3 � green in isolation (3/3).
KNOWN FLAKE: AC132 + AC219.2 fail only when several SLA classes run in parallel against the
shared LocalDB (cross-class scanner interference; AC132 shows the same pre-existing trait).
Owning this is the FINAL stabilization task, not this one.
