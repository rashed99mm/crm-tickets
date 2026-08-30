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

        // CC-10/CC-13 — an agent reply on the WhatsApp/SMS channels leaves through the same
        // notification gateway that outbound system notifications use, quoted to the customer's
        // own phone number. Email and System replies keep their existing paths.
        if (request.Direction == "Outbound" && request.Channel is "WhatsApp" or "SMS")
        {
            var customer = await customers.GetByIdAsync(ticket.CustomerId, ct);
            var phone = customer?.Phone;

            if (!string.IsNullOrWhiteSpace(phone))
            {
                await notificationGateway.SendAsync(new NotificationDispatchRequest(
                    TemplateCode: "TICKET_REPLY",
                    RecipientUserId: null,
                    Channels: [NotificationChannel.Create(request.Channel)],
                    Variables: new Dictionary<string, string> { ["Title"] = "Ticket reply", ["Message"] = request.Body },
                    Email: null,
                    PhoneNumber: phone,
                    BypassUserSettings: true,
                    DeduplicationKey: null,
                    CorrelationId: request.TicketId.ToString()), ct);
            }
            else
            {
                logger.LogWarning(
                    "Outbound {Channel} reply {MessageId} for ticket {TicketId} had no customer phone to send to",
                    request.Channel, message.Id, request.TicketId);
            }
        }

        return messageFactory.Success(message.Id, ApplicationErrors.Ticket.MESSAGE_RECORDED);
    }
}
