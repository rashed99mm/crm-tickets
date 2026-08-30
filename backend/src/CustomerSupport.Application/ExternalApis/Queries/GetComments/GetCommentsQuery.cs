using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.ExternalApis.Clients;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.ExternalApis.Queries.GetComments;

public record GetCommentsQuery(int PostId) : IQuery<Response<List<CommentDto>>>;

public class GetCommentsQueryHandler(
    IPlaceholderClient placeholderClient,
    IMessageFactory messages)
    : IQueryHandler<GetCommentsQuery, Response<List<CommentDto>>>
{
    public async Task<Response<List<CommentDto>>> Handle(GetCommentsQuery request, CancellationToken ct)
    {
        try
        {
            var comments = await placeholderClient.GetCommentsAsync(request.PostId, ct);
            var mapped = comments.Select(c => new CommentDto
            {
                Id = c.Id,
                PostId = c.PostId,
                Name = c.Name,
                Email = c.Email,
                Body = c.Body
            }).ToList();
            return messages.Success(mapped, ApplicationErrors.General.SUCCESS_OPERATION);
        }
        catch
        {
            return messages.Fail<List<CommentDto>>(ApplicationErrors.General.INTERNAL_ERROR, MessageType.Internal);
        }
    }
}
