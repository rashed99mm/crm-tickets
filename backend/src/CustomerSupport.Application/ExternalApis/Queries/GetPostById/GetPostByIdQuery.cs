using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.ExternalApis.Clients;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.ExternalApis.Queries.GetPostById;

public record GetPostByIdQuery(int Id) : IQuery<Response<PostDto>>;

public class GetPostByIdQueryHandler(
    IPlaceholderClient placeholderClient,
    IMessageFactory messages)
    : IQueryHandler<GetPostByIdQuery, Response<PostDto>>
{
    public async Task<Response<PostDto>> Handle(GetPostByIdQuery request, CancellationToken ct)
    {
        try
        {
            var post = await placeholderClient.GetPostByIdAsync(request.Id, ct);
            var mapped = new PostDto
            {
                Id = post.Id,
                UserId = post.UserId,
                Title = post.Title,
                Body = post.Body
            };
            return messages.Success(mapped, ApplicationErrors.General.SUCCESS_OPERATION);
        }
        catch
        {
            return messages.Fail<PostDto>(ApplicationErrors.General.INTERNAL_ERROR, MessageType.Internal);
        }
    }
}
