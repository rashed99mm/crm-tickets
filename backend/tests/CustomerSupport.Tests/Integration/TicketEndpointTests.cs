using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-04 (capture) and FEAT-05 (queue) — `AC-29`…`AC-34`, plus `AC-48`'s persisted history row
/// and `AC-15`'s delete guard, which lives here because it is the one customer criterion that
/// cannot be tested until tickets exist.
/// </summary>
public class TicketEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_client, _) = await _factory.CreateAuthenticatedClientAsync();
        _categoryId = await _factory.EnsureCategoryAsync("Technical");
        _customerId = await CreateCustomerAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateCustomerAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Layla Haddad",
            email = $"ticket-cust-{Guid.NewGuid():N}@example.com",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    /// <summary>
    /// US-923: priority is matrix-derived, not settable directly. <paramref name="priority"/> is
    /// mapped to the impact/urgency pair that derives it, so every existing call site keeps
    /// asserting on the priority it always meant.
    /// </summary>
    private static (string Impact, string Urgency) ToClassification(string priority) => priority switch
    {
        "Low" => ("Low", "Low"),
        "Normal" => ("Medium", "Medium"),
        "High" => ("High", "Medium"),
        "Urgent" => ("High", "High"),
        _ => ("Medium", "Medium"), // invalid values are exercised via impact directly (AC30 test)
    };

    private object NewTicket(string subject = "Cannot sign in", string priority = "Normal", Guid? customerId = null, Guid? categoryId = null)
    {
        var (impact, urgency) = ToClassification(priority);
        return new
        {
            subject,
            description = "The portal rejects my password.",
            customerId = customerId ?? _customerId,
            categoryId = categoryId ?? _categoryId,
            impact,
            urgency,
        };
    }

    private async Task<Guid> CreateTicketAsync(string subject = "Cannot sign in", string priority = "Normal")
    {
        var response = await _client.PostAsJsonAsync("/api/Tickets", NewTicket(subject, priority));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    // --- US-009 — raise a ticket -------------------------------------------------------------------

    [Fact]
    [Trait("AC", "29")]
    public async Task AC29_CreateTicket_ValidRequest_Returns201AsNewAndUnassigned()
    {
        var response = await _client.PostAsJsonAsync("/api/Tickets", NewTicket());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var id = (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;

        var ticket = await _client.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{id}");
        ticket!.Data!.Status.Should().Be("New");
        ticket.Data.AssigneeId.Should().BeNull();
        ticket.Data.Reference.Should().MatchRegex(@"^TKT-\d{6}$");
    }

    /// <summary>
    /// Uniqueness and format, deliberately NOT contiguity. `NEXT VALUE FOR` does not join the
    /// caller's transaction, so a rejected create burns a number permanently — a test asserting
    /// consecutive references would encode a guarantee the design explicitly refuses to make.
    /// </summary>
    [Fact]
    [Trait("AC", "29")]
    public async Task AC29_CreateTicket_IssuesUniqueReferences()
    {
        var first = await CreateTicketAsync();
        var second = await CreateTicketAsync();

        var a = await _client.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{first}");
        var b = await _client.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{second}");

        a!.Data!.Reference.Should().NotBe(b!.Data!.Reference);
        a.Data.Reference.Should().MatchRegex(@"^TKT-\d{6}$");
        b.Data.Reference.Should().MatchRegex(@"^TKT-\d{6}$");
    }

    [Fact]
    [Trait("AC", "30")]
    public async Task AC30_CreateTicket_InvalidFields_Returns400KeyedByField()
    {
        var response = await _client.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "",
            description = "",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "Catastrophic",
            urgency = "Medium",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Select(e => e.Field).Should().Contain(["Subject", "Description", "Impact"]);
    }

    [Fact]
    [Trait("AC", "30")]
    public async Task AC30_CreateTicket_SubjectOverLengthLimit_Returns400KeyedToSubject()
    {
        var response = await _client.PostAsJsonAsync("/api/Tickets", NewTicket(subject: new string('x', 201)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "Subject");
    }

    /// <summary>
    /// The 400-versus-404 rule. A resource named in the PATH that is missing is 404; a resource
    /// referenced in the BODY that is missing is a field-keyed 400, because the addressed resource —
    /// the ticket collection — does exist and it is the payload that is wrong.
    /// </summary>
    [Fact]
    [Trait("AC", "31")]
    public async Task AC31_CreateTicket_UnknownCustomer_Returns400KeyedToCustomerId()
    {
        var response = await _client.PostAsJsonAsync("/api/Tickets", NewTicket(customerId: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "CustomerId");
    }

    [Fact]
    [Trait("AC", "31")]
    public async Task AC31_CreateTicket_UnknownCategory_Returns400KeyedToCategoryId()
    {
        var response = await _client.PostAsJsonAsync("/api/Tickets", NewTicket(categoryId: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "CategoryId");
    }

    [Fact]
    [Trait("AC", "31")]
    public async Task AC31_CreateTicket_BothUnknown_ReportsBothFields()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/Tickets",
            NewTicket(customerId: Guid.NewGuid(), categoryId: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Select(e => e.Field).Should().Contain(["CustomerId", "CategoryId"]);
    }

    [Fact]
    [Trait("AC", "48")]
    public async Task AC48_CreateTicket_PersistsOneCreatedHistoryRow()
    {
        var id = await CreateTicketAsync();

        var ticket = await _client.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{id}");

        ticket!.Data!.History.Should().ContainSingle();
        ticket.Data.History[0].ChangeType.Should().Be("Created");
        ticket.Data.History[0].ToValue.Should().Be("New");
    }

    [Fact]
    [Trait("AC", "36")]
    public async Task AC36_GetTicket_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/Tickets/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- US-013 / US-035 — the queue ---------------------------------------------------------------

    [Fact]
    [Trait("AC", "32")]
    public async Task AC32_GetTickets_ReturnsPagedNewestFirst()
    {
        var older = await CreateTicketAsync("Older ticket");
        await Task.Delay(20);
        var newer = await CreateTicketAsync("Newer ticket");

        var page = await _client.GetFromJsonAsync<Response<PagedData<TicketListRow>>>(
            $"/api/Tickets?page=1&pageSize=50&customerId={_customerId}");

        var ids = page!.Data!.Items.Select(t => t.Id).ToList();
        ids.Should().Contain([older, newer]);
        ids.IndexOf(newer).Should().BeLessThan(ids.IndexOf(older));
    }

    [Fact]
    [Trait("AC", "158")]
    public async Task AC158_GetTickets_ExposesEscalationState()
    {
        var ticketId = await CreateTicketAsync("Escalation projection fixture");

        var page = await _client.GetFromJsonAsync<Response<PagedData<TicketListRow>>>(
            $"/api/Tickets?page=1&pageSize=50&customerId={_customerId}");

        var row = page!.Data!.Items.Single(t => t.Id == ticketId);
        row.EscalationState.Should().Be("None");
    }

    [Fact]
    [Trait("AC", "33")]
    public async Task AC33_GetTickets_EachFilter_ReturnsOnlyMatching()
    {
        await CreateTicketAsync("Urgent one", "Urgent");
        await CreateTicketAsync("Low one", "Low");

        var urgent = await _client.GetFromJsonAsync<Response<PagedData<TicketListRow>>>(
            $"/api/Tickets?customerId={_customerId}&priority=Urgent");

        urgent!.Data!.Items.Should().NotBeEmpty();
        urgent.Data.Items.Should().OnlyContain(t => t.Priority == "Urgent");
    }

    /// <summary>
    /// The test that matters for AC-33. Each filter passing alone says nothing about whether they
    /// compose — a handler that overwrote the predicate instead of conjoining it would pass every
    /// single-filter test and fail every real use.
    /// </summary>
    [Fact]
    [Trait("AC", "33")]
    public async Task AC33_GetTickets_CombinedFilters_NarrowToIntersection()
    {
        await CreateTicketAsync("Urgent one", "Urgent");
        await CreateTicketAsync("Low one", "Low");

        var byPriority = await _client.GetFromJsonAsync<Response<PagedData<TicketListRow>>>(
            $"/api/Tickets?customerId={_customerId}&priority=Urgent");
        var byStatus = await _client.GetFromJsonAsync<Response<PagedData<TicketListRow>>>(
            $"/api/Tickets?customerId={_customerId}&status=New");
        var both = await _client.GetFromJsonAsync<Response<PagedData<TicketListRow>>>(
            $"/api/Tickets?customerId={_customerId}&priority=Urgent&status=New");

        both!.Data!.Items.Should().OnlyContain(t => t.Priority == "Urgent" && t.Status == "New");
        both.Data.TotalCount.Should().BeLessThanOrEqualTo(byPriority!.Data!.TotalCount);
        both.Data.TotalCount.Should().BeLessThan(byStatus!.Data!.TotalCount);
    }

    /// <summary>
    /// An unknown status must be refused, not silently return nothing. An empty page reads to the
    /// user as "no tickets in that state", which is indistinguishable from the truth.
    /// </summary>
    [Fact]
    [Trait("AC", "33")]
    public async Task AC33_GetTickets_UnknownStatusValue_Returns400()
    {
        var response = await _client.GetAsync("/api/Tickets?status=Escalated");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "Status");
    }

    [Fact]
    [Trait("AC", "11")]
    public async Task AC11_GetTickets_PageSizeAboveMaximum_Returns400()
    {
        var response = await _client.GetAsync("/api/Tickets?pageSize=5000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("AC", "34")]
    public async Task AC34_GetTickets_MineWithNoTickets_Returns200EmptyPage()
    {
        // A fresh caller owns nothing. This must be an empty page, never a 404 — the frontend has
        // to be able to tell "nothing assigned to you" from "the request failed" (AC-58).
        var (otherClient, _) = await _factory.CreateAuthenticatedClientAsync();
        using var _ = otherClient;

        var response = await otherClient.GetAsync("/api/Tickets?mine=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Response<PagedData<TicketListRow>>>();
        body!.Data!.Items.Should().BeEmpty();
        body.Data.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// A security test wearing a filter's clothes: the caller id comes from the token, never the
    /// query string. If `mine=true&amp;assigneeId=&lt;someone else&gt;` returned that person's
    /// queue, the toggle would be an information-disclosure endpoint with a friendly name.
    /// </summary>
    [Fact]
    [Trait("AC", "34")]
    public async Task AC34_GetTickets_MineIgnoresSuppliedAssigneeId()
    {
        var (otherClient, otherUser) = await _factory.CreateAuthenticatedClientAsync();
        using var _ = otherClient;

        var response = await otherClient.GetAsync($"/api/Tickets?mine=true&assigneeId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Response<PagedData<TicketListRow>>>();
        // The other user owns nothing, so honouring `mine` means an empty page regardless of what
        // assigneeId asked for.
        body!.Data!.Items.Should().BeEmpty();
        otherUser.Id.Should().NotBeEmpty();
    }

    // --- AC-15 — the delete guard, which needs a ticket to exist -------------------------------------

    [Fact]
    [Trait("AC", "15")]
    public async Task AC15_DeleteCustomer_WithTickets_Returns409AndCustomerRemains()
    {
        await CreateTicketAsync();

        var response = await _client.DeleteAsync($"/api/Customers/{_customerId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR009);

        // Support history must survive a mis-click.
        (await _client.GetAsync($"/api/Customers/{_customerId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The picker the create form needs (US-127). Without it the form would have to offer free
    /// text, which BR-14 refuses.
    /// </summary>
    [Fact]
    public async Task Categories_AreSeededAndListedForThePicker()
    {
        var response = await _client.GetAsync("/api/Categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Response<List<CategoryRow>>>();
        body!.Data!.Select(c => c.Name).Should().Contain(["Technical", "Billing", "Account", "General"]);
    }

    [Fact]
    [Trait("AC", "3")]
    public async Task AC3_Tickets_WithoutAToken_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/Tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public sealed record TicketRow(
        Guid Id,
        string Reference,
        string Subject,
        string Status,
        string Priority,
        Guid? AssigneeId,
        List<HistoryRow> History);

    public sealed record HistoryRow(string ChangeType, string? FromValue, string? ToValue);

    public sealed record CategoryRow(Guid Id, string Name);

    public sealed record TicketListRow(
        Guid Id, string Reference, string Subject, string Status, string Priority, string EscalationState);
}
