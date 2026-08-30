using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Customers.Queries.GetCustomerNotes;

public class GetCustomerNotesQueryHandler(
    IRepository<CustomerNote> notes,
    IRepository<Customer> customers,
    IIdentityUserService identityUsers,
    IMessageFactory messages)
    : IQueryHandler<GetCustomerNotesQuery, Response<PaginatedList<CustomerNoteDto>>>
{
    public async Task<Response<PaginatedList<CustomerNoteDto>>> Handle(
        GetCustomerNotesQuery request,
        CancellationToken ct)
    {
        if (!await customers.ExistsAsync(c => c.Id == request.CustomerId, ct))
        {
            return messages.NotFound<PaginatedList<CustomerNoteDto>>(ApplicationErrors.Customer.NOT_FOUND);
        }

        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Max(request.PageSize, 1);

        var allNotes = await notes.ListAsync(n => n.CustomerId == request.CustomerId, ct);
        var ordered = allNotes.OrderByDescending(n => n.CreatedAt).ToList();
        var total = ordered.Count;

        var rows = ordered
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var authorIds = rows.Select(n => n.AuthorId).Distinct().ToList();
        var authorNames = new Dictionary<Guid, string>();
        foreach (var authorId in authorIds)
        {
            var author = await identityUsers.FindByIdAsync(authorId, ct);
            authorNames[authorId] = author?.FullName ?? string.Empty;
        }

        var items = rows.Select(n => new CustomerNoteDto(
            n.Id,
            n.Body,
            n.AuthorId,
            authorNames.GetValueOrDefault(n.AuthorId, string.Empty),
            n.CreatedAt)).ToList();

        return Response<PaginatedList<CustomerNoteDto>>.Ok(
            PaginatedList<CustomerNoteDto>.Create(items, total, pageIndex, pageSize),
            SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
