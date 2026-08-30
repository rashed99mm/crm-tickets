using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;

/// <summary>
/// CC-1..CC-4. Resolves or creates the customer, resolves or creates the open non-terminal ticket
/// for (customer, channel), and appends the inbound message — one command shared by WhatsApp, SMS
/// and web-form controllers after each parses its own provider payload shape. The ticket-creation
/// half mirrors <c>CreateTicketCommandHandler</c>; the message-append half mirrors
/// <c>RecordTicketMessageCommandHandler</c>; this command fuses the two for a path that has no
/// authenticated <c>IUserContext.UserId</c> to call either with directly.
/// </summary>
public class IngestInboundChannelMessageCommandHandler(
    IRepository<Customer> customers,
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IRepository<Category> categories,
    ITicketReferenceGenerator references,
    IUnitOfWork unitOfWork,
    IMessageFactory messageFactory)
    : ICommandHandler<IngestInboundChannelMessageCommand, Response<Guid>>
{
    private const string DefaultCategoryName = "General";

    public async Task<Response<Guid>> Handle(IngestInboundChannelMessageCommand request, CancellationToken ct)
    {
        // CC-9/CC-12 idempotency: a retried webhook with the same provider message id is a no-op
        // success, not a duplicate insert and not an error.
        if (request.ProviderMessageId is not null)
        {
            var existing = await messages.FirstOrDefaultAsync(
                m => m.Channel == request.Channel && m.ProviderMessageId == request.ProviderMessageId, ct);
            if (existing is not null)
            {
                return messageFactory.Success(existing.Id, ApplicationErrors.Ticket.MESSAGE_RECORDED);
            }
        }

        var customer = await ResolveOrCreateCustomerAsync(request, ct);

        // CC-2/CC-11 — one open ticket per (customer, channel). A terminal ticket starts a new one.
        var nonTerminalTicket = await tickets.FirstOrDefaultAsync(
            t => t.CustomerId == customer.Id
                 && t.Source == request.Channel
                 && t.Status != "Resolved"
                 && t.Status != "Closed",
            ct);

        Guid ticketId;
        if (nonTerminalTicket is not null)
        {
            ticketId = nonTerminalTicket.Id;
        }
        else
        {
            var category = await categories.FirstOrDefaultAsync(c => c.Name == DefaultCategoryName && c.IsActive, ct);
            if (category is null)
            {
                // The external host deliberately does not run staff reference-data seeding. Create
                // the fallback category on first inbound delivery instead of returning a 500.
                category = Category.Create(DefaultCategoryName);
                await categories.AddAsync(category, ct);
            }

            var reference = await references.NextAsync(ct);
            var ticket = Ticket.Create(
                reference,
                subject: $"{request.Channel} — {request.CustomerName ?? "New contact"}",
                description: request.Body,
                customerId: customer.Id,
                categoryId: category.Id,
                priority: "Normal",
                actorId: SystemActors.ChannelIngestion);
            ticket.SetSource(request.Channel);

            await tickets.AddAsync(ticket, ct);
            ticketId = ticket.Id;
        }

        var message = TicketMessage.Create(
            ticketId, "Inbound", request.Channel, subject: null, body: request.Body,
            senderId: SystemActors.ChannelIngestion, providerMessageId: request.ProviderMessageId);

        await messages.AddAsync(message, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messageFactory.Success(message.Id, ApplicationErrors.Ticket.MESSAGE_RECORDED);
    }

    /// <summary>
    /// Matches an existing customer by the strongest identifier present (phone first for WhatsApp
    /// and SMS, then email for web forms), else creates one. A phone-only customer is given a
    /// deterministic RFC 2606-reserved placeholder email so <see cref="Customer"/>'s non-nullable
    /// email contract is honoured without inventing a deliverable address (CC-1).
    /// </summary>
    private async Task<Customer> ResolveOrCreateCustomerAsync(IngestInboundChannelMessageCommand request, CancellationToken ct)
    {
        if (request.CustomerPhone is { } phone)
        {
            var byPhone = await customers.FirstOrDefaultAsync(c => c.Phone == phone, ct);
            if (byPhone is not null)
            {
                return byPhone;
            }

            var placeholderEmail = $"{phone}@channel.invalid";
            var created = Customer.Create(request.CustomerName ?? phone, placeholderEmail, phone);
            await customers.AddAsync(created, ct);
            return created;
        }

        if (request.CustomerEmail is { } email)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var byEmail = await customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail, ct);
            if (byEmail is not null)
            {
                return byEmail;
            }

            var created = Customer.Create(request.CustomerName ?? email, email, phone: null);
            await customers.AddAsync(created, ct);
            return created;
        }

        throw new ArgumentException("An inbound channel message needs a phone or an email to match/create a customer.");
    }
}
