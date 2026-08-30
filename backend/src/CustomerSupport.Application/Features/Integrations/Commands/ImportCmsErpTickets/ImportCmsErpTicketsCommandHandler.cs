using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Integrations.Commands.ImportCmsErpTickets;

public sealed class ImportCmsErpTicketsCommandHandler(
    ICmsErpClient erp,
    IRepository<Ticket> tickets,
    IRepository<Customer> customers,
    IRepository<Category> categories,
    ITicketReferenceGenerator references,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<ImportCmsErpTicketsCommand, Response<ImportCmsErpTicketsResult>>
{
    public async Task<Response<ImportCmsErpTicketsResult>> Handle(
        ImportCmsErpTicketsCommand request,
        CancellationToken ct)
    {
        IReadOnlyList<CmsErpTicket> feed;
        try
        {
            feed = await erp.GetTicketsAsync(ct);
        }
        catch (HttpRequestException)
        {
            return messages.Fail<ImportCmsErpTicketsResult>("INT001", MessageType.Internal);
        }
        var category = (await categories.ListAsync(c => c.IsActive, ct))
            .OrderBy(c => c.CreatedAt)
            .FirstOrDefault();

        if (category is null)
        {
            return messages.Fail<ImportCmsErpTicketsResult>("INT003", MessageType.Validation);
        }

        var imported = 0;
        var skipped = 0;
        var createdReferences = new List<string>();

        foreach (var item in feed.Where(IsValid))
        {
            var marker = $"[CMS-ERP:{item.ExternalId}]";
            if (await tickets.ExistsAsync(t => t.Subject.StartsWith(marker), ct))
            {
                skipped++;
                continue;
            }

            var email = item.CustomerEmail.Trim().ToLowerInvariant();
            var customer = await customers.FirstOrDefaultAsync(c => c.Email == email, ct);
            if (customer is null)
            {
                customer = Customer.Create(item.CustomerName, email, null);
                await customers.AddAsync(customer, ct);
            }

            var ticket = Ticket.Create(
                await references.NextAsync(ct),
                $"{marker} {item.Subject}".Trim(),
                item.Description,
                customer.Id,
                category.Id,
                item.Priority,
                userContext.UserId);
            ticket.SetSource("CMS-ERP");
            await tickets.AddAsync(ticket, ct);
            createdReferences.Add(ticket.Reference);
            imported++;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return messages.Success(
            new ImportCmsErpTicketsResult(imported, skipped, createdReferences),
            "Integration.Imported");
    }

    private static bool IsValid(CmsErpTicket item) =>
        !string.IsNullOrWhiteSpace(item.ExternalId)
        && !string.IsNullOrWhiteSpace(item.CustomerName)
        && !string.IsNullOrWhiteSpace(item.CustomerEmail)
        && !string.IsNullOrWhiteSpace(item.Subject)
        && !string.IsNullOrWhiteSpace(item.Description);
}
