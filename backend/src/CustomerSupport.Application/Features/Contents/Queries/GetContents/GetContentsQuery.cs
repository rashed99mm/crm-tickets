using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Contents.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Contents.Queries.GetContents;

public class GetContentsQuery : BasePagedQuery, IQuery<Response<PaginatedList<ContentDto>>>
{
    public string? SearchTerm { get; init; }
    public string? Status { get; init; }
    public Guid? AuthorId { get; init; }
    public Guid? CategoryId { get; init; }

    public GetContentsQuery()
    {
        PageIndex = 1;
        PageSize = 10;
    }
}
