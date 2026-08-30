using CustomerSupport.Application.Events;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Events.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Tickets.Events;

public sealed class TicketStatusChangedEventHandler(
    IRepository<Ticket> tickets,
    IIdentityUserService users,
    INotificationGateway gateway,
    ILogger<TicketStatusChangedEventHandler> logger) : IDomainEventHandler<TicketStatusChangedEvent>
{
    private readonly IRepository<Ticket> _tickets = tickets;
    private readonly IIdentityUserService _users = users;
    private readonly INotificationGateway _gateway = gateway;
    private readonly ILogger<TicketStatusChangedEventHandler> _logger = logger;

    public async Task Handle(TicketStatusChangedEvent @event, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByIdAsync(@event.TicketId, ct);
        if (ticket is null)
        {
            _logger.LogWarning("Ticket {TicketId} was not found while handling status event.", @event.TicketId);
            return;
        }

        var recipients = new Dictionary<Guid, string>();
        var customerUser = await _users.FindByCustomerIdAsync(ticket.CustomerId, ct);
        if (customerUser is not null)
        {
            recipients.TryAdd(customerUser.Id, "customer");
        }
        else
        {
            _logger.LogDebug("No portal user linked to customer {CustomerId}; status customer leg skipped.", ticket.CustomerId);
        }

        if (ticket.AssigneeId is { } assigneeId)
        {
            recipients.TryAdd(assigneeId, "assignee");
        }

        if (@event.ActorId != Guid.Empty)
        {
            recipients.TryAdd(@event.ActorId, "actor");
        }

        // Workflow changes have a system actor, so include the support roles to keep the admin
        // workspace informed even when nobody is assigned yet.
        foreach (var role in new[] { ApplicationRole.Roles.Admin, ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Agent })
        {
            var supportUsers = await _users.GetUsersInRoleAsync(role, ct) ?? [];
            foreach (var user in supportUsers)
            {
                recipients.TryAdd(user.Id, $"support-{role}");
            }
        }

        foreach (var recipient in recipients)
        {
            await _gateway.SendAsync(
                new NotificationDispatchRequest(
                    TemplateCode: "TICKET_STATUS_CHANGED",
                    RecipientUserId: recipient.Key,
                    Channels: [NotificationChannel.InApp],
                    Variables: new Dictionary<string, string>
                    {
                        ["Title"] = "Ticket updated",
                        ["Message"] = $"Ticket {@event.Reference} moved from {@event.From} to {@event.To}."
                    },
                    Email: null,
                    PhoneNumber: null,
                    BypassUserSettings: true,
                    DeduplicationKey: $"ticket-status:{@event.TicketId}:{@event.To}:{recipient.Key}",
                    CorrelationId: @event.TicketId.ToString()),
                ct);
        }
    }
}
