namespace CustomerSupport.Domain.Events.Content;

public sealed class ContentPublishedEvent : DomainEvent
{
    public Guid ContentId { get; }
    public string Title { get; }
    public Guid AuthorId { get; }
    public string ContentType { get; }

    public ContentPublishedEvent(Guid contentId, string title, Guid authorId, string contentType)
    {
        ContentId = contentId;
        Title = title;
        AuthorId = authorId;
        ContentType = contentType;
    }
}
