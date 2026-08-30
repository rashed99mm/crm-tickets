using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Application.Features.Portal.Commands.CreatePortalReply;

/// <summary>Appends a customer-authored message to a ticket they own (US-407, PJ-10).</summary>
public class CreatePortalReplyCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messageFactory,
    INotificationGateway? notificationGateway = null)
    : ICommandHandler<CreatePortalReplyCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreatePortalReplyCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(request.TicketId, ct);
        if (ticket is null)
        {
            return messageFactory.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        if (ticket.CustomerId != request.CustomerId)
        {
            return messageFactory.Fail<Guid>(ApplicationErrors.General.FORBIDDEN, MessageType.Forbidden);
        }

        // PJ-10. A customer message is "Inbound" on the Portal channel, sent by the signed-in user
        // (spec A5 — the platform has no customer login shadow, so Direction/Channel carry who-spoke).
        var message = TicketMessage.Create(
            ticket.Id, "Inbound", "Portal", null, request.Body, userContext.UserId);

        await messages.AddAsync(message, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var recipientId = ticket.AssigneeId ?? ticket.CreatedBy;
        if (recipientId is Guid agentId && agentId != userContext.UserId)
        {
            if (notificationGateway is not null)
            {
                await notificationGateway.SendAsync(new NotificationDispatchRequest(
                TemplateCode: "TICKET_REPLY",
                RecipientUserId: agentId,
                Channels: [NotificationChannel.InApp],
                Variables: new Dictionary<string, string>
                {
                    ["Title"] = "Customer replied",
                    ["Message"] = $"Ticket {ticket.Reference} has a new customer reply."
                },
                Email: null,
                PhoneNumber: null,
                BypassUserSettings: true,
                DeduplicationKey: $"ticket-reply:{ticket.Id}:agent:{message.Id}",
                    CorrelationId: ticket.Id.ToString()), ct);
            }
        }

        return messageFactory.Success(message.Id, ApplicationErrors.General.SUCCESS_CREATED);
    }
}
