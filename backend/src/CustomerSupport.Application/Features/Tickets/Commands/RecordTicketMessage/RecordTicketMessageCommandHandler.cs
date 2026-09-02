using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Tickets.Commands.RecordTicketMessage;

public class RecordTicketMessageCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IRepository<Customer> customers,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IMessageFactory messageFactory,
    IIdentityUserService identityUsers,
    INotificationGateway notificationGateway,
    ILogger<RecordTicketMessageCommandHandler> logger)
    : ICommandHandler<RecordTicketMessageCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(RecordTicketMessageCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetTrackedAsync(request.TicketId, ct);
        if (ticket is null)
        {
            return messageFactory.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var message = TicketMessage.Create(
            request.TicketId, request.Direction, request.Channel, request.Subject, request.Body, userContext.UserId);

        await messages.AddAsync(message, ct);

        if (request.Direction == "Outbound")
        {
            ticket.RecordResponse(DateTime.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(ct);

        if (request.Direction == "Outbound")
        {
            var customerUser = await identityUsers.FindByCustomerIdAsync(ticket.CustomerId, ct);
            if (customerUser is not null && customerUser.Id != userContext.UserId)
            {
                await notificationGateway.SendAsync(new NotificationDispatchRequest(
                    TemplateCode: "TICKET_REPLY",
                    RecipientUserId: customerUser.Id,
                    Channels: [NotificationChannel.InApp],
                    Variables: new Dictionary<string, string>
                    {
                        ["Title"] = "New ticket reply",
                        ["Message"] = $"Ticket {ticket.Reference} has a new reply from the support team."
                    },
                    Email: null,
                    PhoneNumber: null,
                    BypassUserSettings: true,
                    DeduplicationKey: $"ticket-reply:{ticket.Id}:customer:{message.Id}",
                    CorrelationId: ticket.Id.ToString()), ct);
            }
        }

        // CC-10/CC-13/CC-44 — an agent reply on a customer-facing channel leaves through the same
        // notification gateway outbound system notifications use. The contact field is per channel
        // and never both (RequestOtpCommandHandler.cs:83-92 is the precedent): phone channels carry
        // PhoneNumber, email carries Email. Dispatching email with PhoneNumber set — which adding
        // "Email" to the old channel gate alone would have done — reaches nobody (spec A27).
        if (request.Direction == "Outbound"
            && request.Channel is ChannelNames.WhatsApp or ChannelNames.Sms or ChannelNames.Email)
        {
            var customer = await customers.GetByIdAsync(ticket.CustomerId, ct);
            var isEmail = request.Channel == ChannelNames.Email;

            // A phone-only customer's email is a deterministic {phone}@channel.invalid placeholder
            // (IngestInboundChannelMessageCommandHandler.cs:115) that exists only to satisfy
            // Customer.Email's non-nullable contract. It is not deliverable.
            var email = isEmail && customer?.Email is { } candidate
                        && !candidate.EndsWith("@channel.invalid", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : null;
            var phone = isEmail ? null : customer?.Phone;
            var contact = isEmail ? email : phone;

            if (!string.IsNullOrWhiteSpace(contact))
            {
                await notificationGateway.SendAsync(new NotificationDispatchRequest(
                    TemplateCode: "TICKET_REPLY",
                    RecipientUserId: null,
                    Channels: [NotificationChannel.Create(request.Channel)],
                    Variables: new Dictionary<string, string> { ["Title"] = "Ticket reply", ["Message"] = request.Body },
                    Email: email,
                    PhoneNumber: phone,
                    BypassUserSettings: true,
                    DeduplicationKey: null,
                    CorrelationId: request.TicketId.ToString()), ct);
            }
            else
            {
                logger.LogWarning(
                    "Outbound {Channel} reply {MessageId} for ticket {TicketId} had no deliverable customer contact to send to",
                    request.Channel, message.Id, request.TicketId);
            }
        }

        return messageFactory.Success(message.Id, ApplicationErrors.Ticket.MESSAGE_RECORDED);
    }
}
