namespace CustomerSupport.Application.Interfaces;

public sealed record CmsErpTicket(
    string ExternalId,
    string CustomerName,
    string CustomerEmail,
    string Subject,
    string Description,
    string Priority);

public interface ICmsErpClient
{
    Task<IReadOnlyList<CmsErpTicket>> GetTicketsAsync(CancellationToken ct = default);
}
