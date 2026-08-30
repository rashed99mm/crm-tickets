namespace CustomerSupport.Domain.Events.Content;

public sealed class ContentArchivedEvent : DomainEvent
{
    public Guid ContentId { get; }
    public string Title { get; }

    public ContentArchivedEvent(Guid contentId, string title)
    {
        ContentId = contentId;
        Title = title;
    }
}
