using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Services;

public interface IContentDomainService
{
    Content CreateContent(
        string title,
        string body,
        string contentType,
        Guid authorId,
        string? summary = null,
        string? category = null);
}

public class ContentDomainService : IContentDomainService
{
    public Content CreateContent(
        string title,
        string body,
        string contentType,
        Guid authorId,
        string? summary = null,
        string? category = null)
    {
        return Content.Create(title, body, contentType, authorId, summary, category);
    }
}
