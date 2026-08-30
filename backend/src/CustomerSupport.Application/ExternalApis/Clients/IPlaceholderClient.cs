using CustomerSupport.Application.ExternalApis;
using Refit;

namespace CustomerSupport.Application.ExternalApis.Clients;

[ExternalApiClient("PlaceholderApi")]
public interface IPlaceholderClient
{
    [Get("/posts")]
    Task<List<PlaceholderPostDto>> GetPostsAsync(CancellationToken cancellationToken = default);

    [Get("/posts/{id}")]
    Task<PlaceholderPostDto> GetPostByIdAsync(int id, CancellationToken cancellationToken = default);

    [Get("/posts/{id}/comments")]
    Task<List<PlaceholderCommentDto>> GetCommentsAsync(int id, CancellationToken cancellationToken = default);
}

public class PlaceholderPostDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class PlaceholderCommentDto
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
