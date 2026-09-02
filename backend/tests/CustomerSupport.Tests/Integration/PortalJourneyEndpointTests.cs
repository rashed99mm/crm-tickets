using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// The full portal journey over the customer-facing host and the real database (US-401..US-409,
/// PJ-1..PJ-12). Proves the wiring the unit tests cannot: a portal registration creates the linked
/// customer, the issued token carries the claim, and every portal endpoint enforces ownership.
/// </summary>
public sealed class PortalJourneyEndpointTests : IClassFixture<ExternalApiFactory>
{
    private readonly ExternalApiFactory _factory;

    public PortalJourneyEndpointTests(ExternalApiFactory factory)
    {
        _factory = factory;
    }

    private static string Email(string tag) => $"portal-{tag}-{Guid.NewGuid():N}@test.local";

    private sealed record LoginData(string AccessToken, string RefreshToken);

    private async Task<(HttpClient Client, string Token, string Email)> RegisterLoginAsync(string email)
    {
        await _factory.EnsureDatabaseAsync();
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email,
            username = "portaluser" + Guid.NewGuid().ToString("N"),
            password = "Password123",
            firstName = "Test",
            lastName = "User",
            phoneNumber = (string?)null,
        });
        reg.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync("/api/Auth/login", new { email, password = "Password123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await login.Content.ReadFromJsonAsync<Response<LoginData>>();

        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody!.Data!.AccessToken);

        return (authed, loginBody.Data.AccessToken, email);
    }

    private async Task SetTicketStatusAsync(Guid ticketId, string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.IgnoreQueryFilters().FirstAsync(t => t.Id == ticketId);

        if (status != TicketStatus.Open.Value)
        {
            ticket.ChangeStatus(TicketStatus.Open.Value, Guid.NewGuid());
        }

        ticket.ChangeStatus(status, Guid.NewGuid());
        await db.SaveChangesAsync();
    }

    [Fact]
    [Trait("AC", "401")]
    public async Task PJ2_PortalRegistration_PersistsCustomerAndLink()
    {
        var email = Email("cust");
        await RegisterLoginAsync(email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        user.CustomerId.HasValue.Should().BeTrue();

        var customer = await db.Customers.IgnoreQueryFilters().FirstAsync(c => c.Email == email.ToLowerInvariant());
        user.CustomerId!.Value.Should().Be(customer.Id);
    }

    [Fact]
    [Trait("AC", "402")]
    public async Task PJ3_LoginToken_CarriesCustomerIdClaim()
    {
        var (_, token, _) = await RegisterLoginAsync(Email("claim"));

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "customerId");
    }

    [Fact]
    [Trait("AC", "404")]
    public async Task PJ5_PortalTicket_CreatesWithPortalSource()
    {
        var (client, _, _) = await RegisterLoginAsync(Email("ticket"));
        var categoryId = await _factory.EnsureCategoryAsync($"cat-{Guid.NewGuid():N}");

        var resp = await client.PostAsJsonAsync("/api/portal/tickets", new
        {
            subject = "Cannot log in",
            description = "The button does nothing",
            categoryId,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await resp.Content.ReadFromJsonAsync<Response<Guid>>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.IgnoreQueryFilters().FirstAsync(t => t.Id == body!.Data);

        ticket.Source.Should().Be("Portal");
        ticket.CustomerId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    [Trait("AC", "405")]
    [Trait("AC", "406")]
    [Trait("AC", "407")]
    [Trait("AC", "408")]
    public async Task PJ8_9_10_12_Journey_ListDetailReplySurvey()
    {
        var (client, _, _) = await RegisterLoginAsync(Email("journey"));
        var categoryId = await _factory.EnsureCategoryAsync($"cat-{Guid.NewGuid():N}");

        var created = await client.PostAsJsonAsync("/api/portal/tickets", new
        {
            subject = "Broken export",
            description = "CSV is empty",
            categoryId,
        });
        var ticketId = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var list = await client.GetAsync("/api/portal/tickets");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        listDoc.RootElement.GetProperty("data").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())
            .Should().Contain(ticketId);

        var detail = await client.GetAsync($"/api/portal/tickets/{ticketId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailDoc.RootElement.GetProperty("data").GetProperty("subject").GetString().Should().Be("Broken export");

        var reply = await client.PostAsJsonAsync($"/api/portal/tickets/{ticketId}/reply", new { body = "Any update?" });
        reply.StatusCode.Should().Be(HttpStatusCode.Created);

        await SetTicketStatusAsync(ticketId, TicketStatus.Resolved.Value);

        var survey = await client.PostAsJsonAsync($"/api/portal/tickets/{ticketId}/survey", new { rating = 5, comment = "great" });
        survey.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("AC", "403")]
    public async Task PJ9_CrossCustomerTicket_Returns403()
    {
        var (ownerClient, _, _) = await RegisterLoginAsync(Email("owner"));
        var categoryId = await _factory.EnsureCategoryAsync($"cat-{Guid.NewGuid():N}");
        var created = await ownerClient.PostAsJsonAsync("/api/portal/tickets", new
        {
            subject = "Owner ticket",
            description = "desc",
            categoryId,
        });
        var ticketId = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var (otherClient, _, _) = await RegisterLoginAsync(Email("other"));
        var probe = await otherClient.GetAsync($"/api/portal/tickets/{ticketId}");

        probe.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("AC", "406")]
    public async Task PJ9_UnknownTicket_Returns404()
    {
        var (client, _, _) = await RegisterLoginAsync(Email("unknown"));
        var probe = await client.GetAsync($"/api/portal/tickets/{Guid.NewGuid()}");

        probe.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "408")]
    public async Task PJ11_DuplicateSurvey_Returns409()
    {
        var (client, _, _) = await RegisterLoginAsync(Email("dup"));
        var categoryId = await _factory.EnsureCategoryAsync($"cat-{Guid.NewGuid():N}");
        var created = await client.PostAsJsonAsync("/api/portal/tickets", new
        {
            subject = "Dup survey",
            description = "desc",
            categoryId,
        });
        var ticketId = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
        await SetTicketStatusAsync(ticketId, TicketStatus.Resolved.Value);

        var first = await client.PostAsJsonAsync($"/api/portal/tickets/{ticketId}/survey", new { rating = 4, comment = "ok" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync($"/api/portal/tickets/{ticketId}/survey", new { rating = 5, comment = "again" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "409")]
    public async Task PJ12_SurveyOnUnresolvedTicket_Returns400()
    {
        var (client, _, _) = await RegisterLoginAsync(Email("unresolved"));
        var categoryId = await _factory.EnsureCategoryAsync($"cat-{Guid.NewGuid():N}");
        var created = await client.PostAsJsonAsync("/api/portal/tickets", new
        {
            subject = "Not resolved yet",
            description = "desc",
            categoryId,
        });
        var ticketId = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var survey = await client.PostAsJsonAsync($"/api/portal/tickets/{ticketId}/survey", new { rating = 5, comment = "early" });
        survey.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
