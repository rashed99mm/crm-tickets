using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-03 — customer records, against a real database. `AC-7`…`AC-16`.
///
/// Every test here needs the database to mean something: the duplicate-email conflict is a filtered
/// unique index, the delete guard is a cross-table read, and the "deleted email becomes reusable"
/// criterion is the index's filter clause doing its job. None of that is observable in memory.
/// </summary>
public class CustomerEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_client, _) = await _factory.CreateAuthenticatedClientAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private static object NewCustomer(string? email = null, string name = "Layla Haddad") => new
    {
        name,
        email = email ?? $"layla-{Guid.NewGuid():N}@example.com",
        phone = "+20 100 000 0000",
    };

    private async Task<Guid> CreateCustomerAsync(string? email = null, string name = "Layla Haddad")
    {
        var response = await _client.PostAsJsonAsync("/api/Customers", NewCustomer(email, name));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        return body!.Data!;
    }

    // --- US-001 — create -------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "7")]
    public async Task AC7_CreateCustomer_ValidRequest_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/Customers", NewCustomer());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("AC", "8")]
    public async Task AC8_CreateCustomer_InvalidFields_Returns400KeyedByField()
    {
        var response = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "",
            email = "not-an-email",
            phone = new string('9', 40),
        });

        // 400, not 422 — ADR-0011.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().NotBeNull();
        // Keyed by field, and every offending field reported in ONE response — a form that has to
        // submit three times to learn about three problems is the failure AC-8 is guarding against.
        body.Errors.Select(e => e.Field).Should().Contain(["Name", "Email", "Phone"]);
    }

    [Fact]
    [Trait("AC", "8")]
    public async Task AC8_CreateCustomer_NameOverLengthLimit_Returns400KeyedToName()
    {
        var response = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = new string('x', 201),
            email = $"long-{Guid.NewGuid():N}@example.com",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "Name");
    }

    // --- US-116 — duplicate email is a conflict --------------------------------------------------

    [Fact]
    [Trait("AC", "9")]
    public async Task AC9_CreateCustomer_DuplicateEmail_Returns409NotValidationError()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await CreateCustomerAsync(email);

        var response = await _client.PostAsJsonAsync("/api/Customers", NewCustomer(email));

        // The whole point of this criterion: a duplicate is not a malformed request.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR008);
    }

    [Fact]
    [Trait("AC", "9")]
    public async Task AC9_CreateCustomer_DuplicateEmailDifferentCase_Returns409()
    {
        var email = $"case-{Guid.NewGuid():N}@example.com";
        await CreateCustomerAsync(email);

        var response = await _client.PostAsJsonAsync("/api/Customers", NewCustomer(email.ToUpperInvariant()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // --- US-004 — find ---------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "10")]
    public async Task AC10_GetCustomers_ReturnsPagedEnvelope()
    {
        await CreateCustomerAsync();

        var response = await _client.GetAsync("/api/Customers?page=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Response<PagedData<CustomerRow>>>();
        body!.Data!.PageSize.Should().Be(5);
        body.Data.PageIndex.Should().Be(1);
        body.Data.TotalCount.Should().BeGreaterThan(0);
        body.Data.Items.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("AC", "11")]
    public async Task AC11_GetCustomers_PageSizeAboveMaximum_Returns400()
    {
        var response = await _client.GetAsync("/api/Customers?page=1&pageSize=5000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "PageSize");
    }

    [Fact]
    [Trait("AC", "13")]
    public async Task AC13_GetCustomers_SearchTerm_MatchesNameOrEmail()
    {
        var marker = Guid.NewGuid().ToString("N")[..12];
        await CreateCustomerAsync($"{marker}@example.com", $"Zzz {marker}");
        await CreateCustomerAsync();

        var byName = await _client.GetFromJsonAsync<Response<PagedData<CustomerRow>>>($"/api/Customers?search={marker}");
        byName!.Data!.Items.Should().OnlyContain(c => c.Name.Contains(marker) || c.Email.Contains(marker));
        byName.Data.Items.Should().HaveCount(1);

        // Case-insensitively — SQL Server's default collation gives this, and the test pins it so a
        // collation change fails loudly instead of quietly narrowing search results.
        var upperCased = await _client.GetFromJsonAsync<Response<PagedData<CustomerRow>>>(
            $"/api/Customers?search={marker.ToUpperInvariant()}");
        upperCased!.Data!.Items.Should().HaveCount(1);
    }

    /// <summary>
    /// A search matching nothing is an empty page inside an intact envelope — never a 404. The
    /// frontend has to be able to tell "no matches" from "the request failed" (AC-58).
    /// </summary>
    [Fact]
    [Trait("AC", "13")]
    public async Task AC13_GetCustomers_SearchMatchingNothing_ReturnsEmptyPageNotAnError()
    {
        var response = await _client.GetAsync($"/api/Customers?search=no-such-customer-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Response<PagedData<CustomerRow>>>();
        body!.Success.Should().BeTrue();
        body.Data!.Items.Should().BeEmpty();
        body.Data.TotalCount.Should().Be(0);
    }

    // --- US-002 — read and correct ---------------------------------------------------------------

    [Fact]
    [Trait("AC", "12")]
    public async Task AC12_GetCustomer_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/Customers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "12")]
    public async Task AC12_UpdateCustomer_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/Customers/{Guid.NewGuid()}", new
        {
            name = "Layla",
            email = $"ghost-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "14")]
    public async Task AC14_UpdateCustomer_ValidChange_Persists()
    {
        var id = await CreateCustomerAsync();
        var newEmail = $"changed-{Guid.NewGuid():N}@example.com";

        var response = await _client.PutAsJsonAsync($"/api/Customers/{id}", new
        {
            name = "Layla Haddad-Corrected",
            email = newEmail,
            phone = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await _client.GetFromJsonAsync<Response<CustomerRow>>($"/api/Customers/{id}");
        reread!.Data!.Name.Should().Be("Layla Haddad-Corrected");
        reread.Data.Email.Should().Be(newEmail);
    }

    [Fact]
    [Trait("AC", "14")]
    public async Task AC14_UpdateCustomer_EmailTakenByAnother_Returns409()
    {
        var takenEmail = $"taken-{Guid.NewGuid():N}@example.com";
        await CreateCustomerAsync(takenEmail);
        var id = await CreateCustomerAsync();

        var response = await _client.PutAsJsonAsync($"/api/Customers/{id}", new
        {
            name = "Layla",
            email = takenEmail,
            phone = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "14")]
    public async Task AC14_UpdateCustomer_InvalidEmail_Returns400()
    {
        var id = await CreateCustomerAsync();

        var response = await _client.PutAsJsonAsync($"/api/Customers/{id}", new
        {
            name = "Layla",
            email = "not-an-email",
            phone = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- US-117 — the delete guard ---------------------------------------------------------------

    [Fact]
    [Trait("AC", "16")]
    public async Task AC16_DeleteCustomer_WithoutTickets_Returns200AndDisappearsFromList()
    {
        var marker = Guid.NewGuid().ToString("N")[..12];
        var id = await CreateCustomerAsync($"{marker}@example.com", $"Zzz {marker}");

        var response = await _client.DeleteAsync($"/api/Customers/{id}");

        // 200, not 204 — FND-5: every response carries a code and a message.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetFromJsonAsync<Response<PagedData<CustomerRow>>>($"/api/Customers?search={marker}");
        list!.Data!.Items.Should().BeEmpty();

        (await _client.GetAsync($"/api/Customers/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The criterion the filtered unique index exists for. A plain unique index would refuse this,
    /// and the conflict would point at a record the user can no longer see.
    /// </summary>
    [Fact]
    [Trait("AC", "16")]
    public async Task AC16_CreateCustomer_EmailOfDeletedCustomer_Succeeds()
    {
        var email = $"reuse-{Guid.NewGuid():N}@example.com";
        var id = await CreateCustomerAsync(email);
        (await _client.DeleteAsync($"/api/Customers/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.PostAsJsonAsync("/api/Customers", NewCustomer(email));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("AC", "12")]
    public async Task AC12_DeleteCustomer_UnknownId_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/Customers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- AC-3 — protected surface ------------------------------------------------------------------

    [Fact]
    [Trait("AC", "3")]
    public async Task AC3_Customers_WithoutAToken_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/Customers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public sealed record CustomerRow(Guid Id, string Name, string Email, string? Phone);
}
