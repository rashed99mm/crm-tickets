using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// MVP-05 — interaction history, against a real database. `AC-74`, `AC-75`, `AC-76`, and the
/// inherited `AC-20`/`AC-11` rules the notes routes must obey like every other paged surface.
///
/// These run over LocalDB rather than in memory for the same reason the rest of the CRM suite does:
/// the ordering criterion is an index doing its job, and the author foreign key is a constraint the
/// in-memory provider would not enforce.
/// </summary>
public class CustomerNotesEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _callerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        var (client, caller) = await _factory.CreateAuthenticatedClientAsync();
        _client = client;
        _callerId = caller.Id;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    /// <summary>A customer of this test's own, so nothing here depends on what another test wrote.</summary>
    private async Task<Guid> CreateCustomerAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Layla Haddad",
            email = $"notes-{Guid.NewGuid():N}@example.com",
            phone = "+20 100 000 0000",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        return body!.Data!;
    }

    private Task<HttpResponseMessage> AddNoteAsync(Guid customerId, string body) =>
        _client.PostAsJsonAsync($"/api/Customers/{customerId}/notes", new { body });

    private async Task<PagedData<CustomerNoteRow>> GetNotesAsync(Guid customerId)
    {
        var response = await _client.GetFromJsonAsync<Response<PagedData<CustomerNoteRow>>>(
            $"/api/Customers/{customerId}/notes");

        return response!.Data!;
    }

    // --- AC-75 — writing a note --------------------------------------------------------------------

    [Fact]
    [Trait("AC", "75")]
    public async Task AC75_AddNote_ValidBody_Returns201AndAppearsInTheList()
    {
        var customerId = await CreateCustomerAsync();

        var response = await AddNoteAsync(customerId, "Called back, awaiting logs.");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        created!.Success.Should().BeTrue();
        created.Data.Should().NotBeEmpty();

        // The criterion is about the note being *readable afterwards*, not about the write
        // answering 201 — an endpoint that returns 201 and stores nothing satisfies neither.
        var notes = await GetNotesAsync(customerId);
        notes.TotalCount.Should().Be(1);
        notes.Items.Single().Body.Should().Be("Called back, awaiting logs.");
        notes.Items.Single().Id.Should().Be(created.Data!);
    }

    [Fact]
    [Trait("AC", "75")]
    public async Task AC75_AddNote_EmptyBody_Returns400KeyedToBody()
    {
        var customerId = await CreateCustomerAsync();

        // Whitespace, not an absent field: the form is what sends this, and a note of three spaces
        // is the same empty record as a note of none.
        var response = await AddNoteAsync(customerId, "   ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.VAL001);
        // Keyed to the field so the note box can show the message on itself rather than in a banner.
        body.Errors.Should().Contain(e => e.Field == "Body");

        (await GetNotesAsync(customerId)).TotalCount.Should().Be(0);
    }

    // --- AC-74 — reading the history ---------------------------------------------------------------

    [Fact]
    [Trait("AC", "74")]
    public async Task AC74_GetNotes_ReturnsNewestFirstWithAuthorNames()
    {
        var customerId = await CreateCustomerAsync();

        (await AddNoteAsync(customerId, "First contact.")).StatusCode
            .Should().Be(HttpStatusCode.Created);

        // The rows are ordered on CreatedAt, which the entity stamps from the clock. Without a gap
        // the two stamps can land inside one tick and the assertion below would be testing nothing.
        await Task.Delay(20);

        (await AddNoteAsync(customerId, "Called back, awaiting logs.")).StatusCode
            .Should().Be(HttpStatusCode.Created);

        var notes = await GetNotesAsync(customerId);

        notes.TotalCount.Should().Be(2);
        notes.Items.Select(n => n.Body).Should()
            .ContainInOrder("Called back, awaiting logs.", "First contact.");
        notes.Items.Should().BeInDescendingOrder(n => n.CreatedAt);

        // Projected at read time from AuthorId — the row stores no name. CrmApiFactory creates its
        // users as "Test"/"User".
        notes.Items.Should().OnlyContain(n => n.AuthorName == "Test User");
        notes.Items.Should().OnlyContain(n => n.AuthorId != Guid.Empty);
    }

    // --- AC-76 — the author is the session, not the payload ----------------------------------------

    /// <summary>
    /// The security criterion. It posts a body that <em>carries</em> an author belonging to somebody
    /// else and asserts the stored note names the caller — a test that merely omitted the field
    /// would pass just as happily against a handler that honoured it.
    /// </summary>
    [Fact]
    [Trait("AC", "76")]
    public async Task AC76_AddNote_AuthorComesFromTheTokenNotThePayload()
    {
        var customerId = await CreateCustomerAsync();
        var (otherUser, _) = await _factory.CreateUserAsync();

        otherUser.Id.Should().NotBe(_callerId);

        var response = await _client.PostAsJsonAsync($"/api/Customers/{customerId}/notes", new
        {
            body = "Injected",
            authorId = otherUser.Id,
            createdBy = otherUser.Id,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var note = (await GetNotesAsync(customerId)).Items.Single();
        note.AuthorId.Should().Be(_callerId);
        note.AuthorId.Should().NotBe(otherUser.Id);
    }

    // --- AC-20 / AC-11 — the rules every route inherits ---------------------------------------------

    /// <summary>
    /// The customer is named in the <b>path</b>, so its absence makes the addressed resource absent:
    /// 404. The contrast is AC-31, where a missing customer in a request <em>body</em> is a 400.
    /// </summary>
    [Fact]
    [Trait("AC", "20")]
    public async Task AC20_AddNote_UnknownCustomer_Returns404()
    {
        var response = await AddNoteAsync(Guid.NewGuid(), "Note against nobody.");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR007);
    }

    [Fact]
    [Trait("AC", "20")]
    public async Task AC20_GetNotes_UnknownCustomer_Returns404()
    {
        var response = await _client.GetAsync($"/api/Customers/{Guid.NewGuid()}/notes");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "11")]
    public async Task AC21_GetNotes_PageSizeAboveMaximum_Returns400()
    {
        var customerId = await CreateCustomerAsync();

        var response = await _client.GetAsync($"/api/Customers/{customerId}/notes?page=1&pageSize=5000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "PageSize");
    }

    [Fact]
    [Trait("AC", "3")]
    public async Task AC3_Notes_WithoutAToken_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/Customers/{Guid.NewGuid()}/notes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>One row of the notes list, exactly as the contract in the spec fixes it.</summary>
    public sealed record CustomerNoteRow(
        Guid Id,
        string Body,
        Guid AuthorId,
        string AuthorName,
        DateTime CreatedAt);
}
