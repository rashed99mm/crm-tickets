using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CustomerSupport.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CustomerSupport.Infrastructure.ExternalApis;

public sealed class CmsErpClient(HttpClient httpClient, IConfiguration configuration) : ICmsErpClient
{
    public async Task<IReadOnlyList<CmsErpTicket>> GetTicketsAsync(CancellationToken ct = default)
    {
        var baseUrl = configuration["Integrations:Cms:ErpBaseUrl"] ?? "http://localhost:3001";
        var feed = await httpClient.GetFromJsonAsync<ErpFeed>(
            $"{baseUrl.TrimEnd('/')}/integrationgateway/erp/tickets", ct);
        return feed?.Tickets ?? [];
    }

    private sealed record ErpFeed(
        [property: JsonPropertyName("tickets")] IReadOnlyList<CmsErpTicket> Tickets);
}
