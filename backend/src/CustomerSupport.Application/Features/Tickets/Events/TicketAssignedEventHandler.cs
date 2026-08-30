using CustomerSupport.Application.Events;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Events.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Tickets.Events;

public sealed class TicketAssignedEventHandler(
    IRepository<Ticket> tickets,
    IIdentityUserService users,
    INotificationGateway gateway,
    ILogger<TicketAssignedEventHandler> logger) : IDomainEventHandler<TicketAssignedEvent>
{
    private readonly IRepository<Ticket> _tickets = tickets;
    private readonly IIdentityUserService _users = users;
    private readonly INotificationGateway _gateway = gateway;
    private readonly ILogger<TicketAssignedEventHandler> _logger = logger;

    public async Task Handle(TicketAssignedEvent @event, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByIdAsync(@event.TicketId, ct);
        if (ticket is null)
        {
            _logger.LogWarning("Ticket {TicketId} was not found while handling assignment event.", @event.TicketId);
            return;
        }

        var customerUser = await _users.FindByCustomerIdAsync(ticket.CustomerId, ct);
        if (customerUser is not null)
        {
            await _gateway.SendAsync(DispatchCustomer(@event, customerUser.Id), ct);
        }
        else
        {
            _logger.LogDebug("No portal user linked to customer {CustomerId}; assignment customer leg skipped.", ticket.CustomerId);
        }

        if (@event.AssigneeId != Guid.Empty && @event.AssigneeId != customerUser?.Id)
        {
            await _gateway.SendAsync(DispatchAssignee(@event, @event.AssigneeId), ct);
        }
    }

    private static NotificationDispatchRequest DispatchCustomer(TicketAssignedEvent @event, Guid recipientId) =>
        new(
            TemplateCode: "TICKET_ASSIGNED",
            RecipientUserId: recipientId,
            Channels: [NotificationChannel.InApp],
            Variables: new Dictionary<string, string>
            {
                ["Title"] = "Ticket assigned",
                ["Message"] = $"Ticket {@event.Reference} has been assigned and is now being handled."
            },
            Email: null,
            PhoneNumber: null,
            BypassUserSettings: true,
            DeduplicationKey: $"ticket-assigned:{@event.TicketId}:customer",
            CorrelationId: @event.TicketId.ToString());

    private static NotificationDispatchRequest DispatchAssignee(TicketAssignedEvent @event, Guid recipientId) =>
        new(
            TemplateCode: "TICKET_ASSIGNED",
            RecipientUserId: recipientId,
            Channels: [NotificationChannel.InApp],
            Variables: new Dictionary<string, string>
            {
                ["Title"] = "Ticket assigned to you",
                ["Message"] = $"Ticket {@event.Reference} has been assigned to you."
            },
            Email: null,
            PhoneNumber: null,
            BypassUserSettings: true,
            DeduplicationKey: $"ticket-assigned:{@event.TicketId}:assignee:{recipientId}",
            CorrelationId: @event.TicketId.ToString());
}
