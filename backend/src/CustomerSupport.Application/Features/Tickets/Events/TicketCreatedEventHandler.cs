using CustomerSupport.Application.Events;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Events.Tickets;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Tickets.Events;

/// <summary>
/// Turns a <see cref="TicketCreatedEvent"/> into an in-app notification for **two** recipients
/// (AC-N1/A1): the ticket's customer, when that customer has a linked portal login (US-401), and the
/// acting staff member who created the ticket (AC-N7). The gateway persists a durable
/// <c>Notification</c> row (Channel InApp) for each recipient before the SignalR push (AC-N2/AC-N3).
/// A customer with no linked user — the normal case for a staff-created record — simply skips the
/// customer leg (AC-N5) and still notifies the creator.
/// </summary>
public sealed class TicketCreatedEventHandler(
    IIdentityUserService users,
    INotificationGateway gateway,
    ILogger<TicketCreatedEventHandler> logger) : IDomainEventHandler<TicketCreatedEvent>
{
    private readonly IIdentityUserService _users = users;
    private readonly INotificationGateway _gateway = gateway;
    private readonly ILogger<TicketCreatedEventHandler> _logger = logger;

    public async Task Handle(TicketCreatedEvent @event, CancellationToken ct = default)
    {
        var customerUser = await _users.FindByCustomerIdAsync(@event.CustomerId, ct);

        // Customer leg (AC-N2/AC-N5): only when the customer has a linked portal login.
        if (customerUser is not null)
        {
            await _gateway.SendAsync(
                Dispatch(@event, customerUser.Id, "customer"), ct);
        }
        else
        {
            _logger.LogDebug("No portal user linked to customer {CustomerId}; customer leg skipped.", @event.CustomerId);
        }

        // Creator leg (AC-N7): the acting staff member who created the ticket. Skipped for an empty
        // actor, and skipped for a self-service portal submission where the creator IS the linked
        // customer — otherwise the same user would be notified twice.
        if (@event.ActorId != Guid.Empty
            && (customerUser is null || customerUser.Id != @event.ActorId))
        {
            await _gateway.SendAsync(
                Dispatch(@event, @event.ActorId, "creator"), ct);
        }

        // Portal submissions have no staff creator. Notify the support team so the ticket appears
        // in the admin notification bell as well as in the shared ticket queue.
        if (customerUser?.Id == @event.ActorId)
        {
            await NotifySupportTeamAsync(@event, customerUser.Id, ct);
        }
    }

    private async Task NotifySupportTeamAsync(TicketCreatedEvent @event, Guid customerUserId, CancellationToken ct)
    {
        var recipients = new Dictionary<Guid, string>();
        foreach (var role in new[] { ApplicationRole.Roles.Admin, ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Agent })
        {
            var users = await _users.GetUsersInRoleAsync(role, ct) ?? [];
            foreach (var user in users)
            {
                if (user.Id != customerUserId)
                {
                    recipients.TryAdd(user.Id, role);
                }
            }
        }

        foreach (var recipient in recipients)
        {
            await _gateway.SendAsync(
                Dispatch(@event, recipient.Key, $"support-{recipient.Value}"), ct);
        }
    }

    private static NotificationDispatchRequest Dispatch(TicketCreatedEvent @event, Guid recipientId, string role) =>
        new(
            TemplateCode: "TICKET_CREATED",
            RecipientUserId: recipientId,
            Channels: [NotificationChannel.InApp],
            Variables: new Dictionary<string, string>
            {
                ["Title"] = "Ticket created",
                ["Message"] = $"Ticket {@event.Reference} has been created.",
            },
            Email: null,
            PhoneNumber: null,
            BypassUserSettings: true,
            DeduplicationKey: $"ticket-created:{@event.TicketId}:{role}",
            CorrelationId: @event.TicketId.ToString());
}
