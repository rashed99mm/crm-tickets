using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.ExternalApis.Clients;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.ExternalApis.Queries.GetPosts;

public record GetPostsQuery : IQuery<Response<List<PostDto>>>;

public class GetPostsQueryHandler(
    IPlaceholderClient placeholderClient,
    IMessageFactory messages)
    : IQueryHandler<GetPostsQuery, Response<List<PostDto>>>
{
    public async Task<Response<List<PostDto>>> Handle(GetPostsQuery request, CancellationToken ct)
    {
        try
        {
            var posts = await placeholderClient.GetPostsAsync(ct);
            var mapped = posts.Select(p => new PostDto
            {
                Id = p.Id,
                UserId = p.UserId,
                Title = p.Title,
                Body = p.Body
            }).ToList();
            return messages.Success(mapped, ApplicationErrors.General.SUCCESS_OPERATION);
        }
        catch
        {
            return messages.Fail<List<PostDto>>(ApplicationErrors.General.INTERNAL_ERROR, MessageType.Internal);
        }
    }
}
