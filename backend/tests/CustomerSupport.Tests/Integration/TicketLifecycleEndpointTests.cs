using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-06 (lifecycle), FEAT-07 (assignment and per-record authorization) and FEAT-08 (history).
/// `AC-35`, `AC-37`…`AC-50`.
///
/// Against a real database throughout. `AC-41` is a `rowversion` and `AC-49` is a `SaveChanges`
/// guard — neither is observable in memory.
/// </summary>
public class TicketLifecycleEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _supervisor = null!;
    private HttpClient _agent = null!;
    private ApplicationUser _agentUser = null!;
    private HttpClient _otherAgent = null!;
    private ApplicationUser _otherAgentUser = null!;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();

        (_supervisor, _) = await _factory.CreateAuthenticatedClientAsync(Roles.Supervisor);
        (_agent, _agentUser) = await _factory.CreateAuthenticatedClientAsync(Roles.Agent);
        (_otherAgent, _otherAgentUser) = await _factory.CreateAuthenticatedClientAsync(Roles.Agent);

        _categoryId = await _factory.EnsureCategoryAsync("Technical");
        _customerId = await CreateCustomerAsync();
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        _agent.Dispose();
        _otherAgent.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private static class Roles
    {
        public const string Agent = "Agent";
        public const string Supervisor = "Supervisor";
    }

    // --- fixtures --------------------------------------------------------------------------------

    private async Task<Guid> CreateCustomerAsync()
    {
        var response = await _supervisor.PostAsJsonAsync("/api/Customers", new
        {
            name = "Layla Haddad",
            email = $"lifecycle-{Guid.NewGuid():N}@example.com",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    private async Task<Guid> CreateTicketAsync()
    {
        var response = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot sign in",
            description = "The portal rejects my password.",
            customerId = _customerId,
            categoryId = _categoryId,
            priority = "Normal",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    private async Task<TicketDetail> DetailAsync(HttpClient client, Guid id)
    {
        var body = await client.GetFromJsonAsync<Response<TicketDetail>>($"/api/Tickets/{id}");
        return body!.Data!;
    }

    private async Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, Guid id, string status, string? rowVersion = null)
    {
        rowVersion ??= (await DetailAsync(_supervisor, id)).RowVersion;
        return await client.PostAsJsonAsync($"/api/Tickets/{id}/status", new { status, rowVersion });
    }

    private async Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid id, Guid assigneeId, string? rowVersion = null)
    {
        rowVersion ??= (await DetailAsync(_supervisor, id)).RowVersion;
        return await client.PostAsJsonAsync($"/api/Tickets/{id}/assignee", new { assigneeId, rowVersion });
    }

    /// <summary>Walks a ticket to a status using the supervisor, who may move any ticket (AC-47).</summary>
    private async Task<Guid> TicketAtAsync(string status)
    {
        var id = await CreateTicketAsync();

        // Assign before entering any work state (AC-505). Supervisor can assign any ticket (AC-42).
        var needsAssignee = status is "In Progress" or "Waiting for Customer" or "Waiting for Internal Team";
        if (needsAssignee)
        {
            (await AssignAsync(_supervisor, id, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        string[] path = status switch
        {
            "New" => [],
            "Open" => ["Open"],
            "Assigned" => ["Open", "Assigned"],
            "In Progress" => ["Open", "Assigned", "In Progress"],
            "Waiting for Customer" => ["Open", "Assigned", "In Progress", "Waiting for Customer"],
            "Waiting for Internal Team" => ["Open", "Assigned", "In Progress", "Waiting for Internal Team"],
            "Resolved" => ["Open", "Resolved"],
            "Closed" => ["Open", "Resolved", "Closed"],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        foreach (var step in path)
        {
            (await ChangeStatusAsync(_supervisor, id, step)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return id;
    }

    // --- US-010 — detail -------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "35")]
    public async Task AC35_GetTicket_ReturnsCustomerSummaryAndHistoryNewestFirst()
    {
        var id = await CreateTicketAsync();
        (await AssignAsync(_supervisor, id, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ChangeStatusAsync(_agent, id, "Open")).StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await DetailAsync(_supervisor, id);

        detail.Customer.Name.Should().Be("Layla Haddad");
        detail.Customer.Email.Should().NotBeNullOrWhiteSpace();
        detail.CategoryName.Should().Be("Technical");

        detail.History.Should().HaveCount(3);
        detail.History.Select(h => h.ChangeType)
            .Should().Equal("StatusChanged", "Assigned", "Created");
        detail.History.Should().BeInDescendingOrder(h => h.OccurredAt);
    }

    // --- US-016 / US-118 — the lifecycle over the wire --------------------------------------------

    [Theory]
    [Trait("AC", "501")]
    [InlineData("New", "Open")]
    [InlineData("Open", "Assigned")]
    [InlineData("Open", "Resolved")]
    [InlineData("Assigned", "In Progress")]
    [InlineData("In Progress", "Waiting for Customer")]
    [InlineData("Waiting for Customer", "In Progress")]
    [InlineData("In Progress", "Resolved")]
    [InlineData("Resolved", "Closed")]
    public async Task AC501_ChangeStatus_8StateMachine_PermittedTransition_Returns200(string from, string to)
    {
        var id = await TicketAtAsync(from);

        var response = await ChangeStatusAsync(_supervisor, id, to);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DetailAsync(_supervisor, id)).Status.Should().Be(to);
    }

    /// <summary>
    /// A refused transition is a <b>conflict</b>, not a validation failure: the request is
    /// well-formed and it is the state that is wrong. That contrast is what AC-38 turns on.
    /// </summary>
    [Theory]
    [Trait("AC", "38")]
    [InlineData("New", "Closed")]
    [InlineData("Closed", "Resolved")]
    [InlineData("New", "Resolved")]
    public async Task AC38_ChangeStatus_UndefinedTransition_Returns409NotValidationError(string from, string to)
    {
        var id = await TicketAtAsync(from);

        var response = await ChangeStatusAsync(_supervisor, id, to);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR013);
    }

    [Fact]
    [Trait("AC", "38")]
    public async Task AC38_RefusedTransition_ChangesNothing()
    {
        var id = await TicketAtAsync("New");
        var before = await DetailAsync(_supervisor, id);

        (await ChangeStatusAsync(_supervisor, id, "Closed")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var after = await DetailAsync(_supervisor, id);
        after.Status.Should().Be(before.Status);
        after.History.Should().HaveCount(before.History.Count);
    }

    [Theory]
    [Trait("AC", "39")]
    [InlineData("New")]
    [InlineData("Open")]
    [InlineData("Resolved")]
    public async Task AC39_ChangeStatus_ToTheStatusAlreadyHeld_Returns409(string status)
    {
        var id = await TicketAtAsync(status);

        var response = await ChangeStatusAsync(_supervisor, id, status);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR014);
    }

    /// <summary>
    /// The distinction that is easiest to lose: `Closed` from `New` is a 409 because the state is
    /// wrong, but `Escalated` is a 400 because there is no such status. Same endpoint, and they
    /// must not answer alike.
    /// </summary>
    [Fact]
    [Trait("AC", "30")]
    public async Task AC30_ChangeStatus_UnknownStatusValue_Returns400NotConflict()
    {
        var id = await TicketAtAsync("New");

        var response = await ChangeStatusAsync(_supervisor, id, "Escalated");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "Status");
    }

    [Fact]
    [Trait("AC", "36")]
    public async Task AC36_ChangeStatus_UnknownTicket_Returns404()
    {
        var response = await _supervisor.PostAsJsonAsync(
            $"/api/Tickets/{Guid.NewGuid()}/status",
            new { status = "Open", rowVersion = Convert.ToBase64String(new byte[8]) });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- US-026 — reopen and concurrency ----------------------------------------------------------

    [Theory]
    [Trait("AC", "503")]
    [InlineData("Resolved")]
    [InlineData("Closed")]
    public async Task AC503_Reopening_RecordsReopenedRow_AndSetsStatusToInProgress(string from)
    {
        var id = await TicketAtAsync(from);

        (await ChangeStatusAsync(_supervisor, id, "In Progress")).StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await DetailAsync(_supervisor, id);
        detail.Status.Should().Be("In Progress");
        detail.History[0].ChangeType.Should().Be("Reopened");
        detail.History[0].FromValue.Should().Be(from);
        detail.History[0].ToValue.Should().Be("In Progress");
    }

    /// <summary>
    /// AC-41. Two callers load the same ticket and both change it; the second must be refused and
    /// the first change must survive. Needs a real `rowversion` — the in-memory provider does not
    /// honour one, and would report this as passing while the database lost the update.
    /// </summary>
    [Fact]
    [Trait("AC", "505")]
    public async Task AC505_UnassignedTicket_EnteringWorkState_Returns409()
    {
        var id = await TicketAtAsync("Assigned");   // walked to Assigned but never assigned
        var response = await ChangeStatusAsync(_supervisor, id, "In Progress");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR013);   // TRANSITION_NOT_ALLOWED
    }

    [Theory]
    [Trait("AC", "505")]
    [InlineData("Resolved")]
    [InlineData("Closed")]
    public async Task AC505_ReopeningUnassignedTicket_Returns409(string from)
    {
        var id = await TicketAtAsync(from);   // walked without assignment; reopening needs assignee
        var response = await ChangeStatusAsync(_supervisor, id, "In Progress");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "533")]
    public async Task AC533_Agent_SelfAssign_FromQueue_Returns200()
    {
        var id = await CreateTicketAsync();

        var response = await AssignAsync(_agent, id, _agentUser.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DetailAsync(_supervisor, id)).AssigneeId.Should().Be(_agentUser.Id);
    }

    [Fact]
    [Trait("AC", "533")]
    public async Task AC533_Agent_AssigningAnotherAgent_Returns403()
    {
        var id = await CreateTicketAsync();

        var response = await AssignAsync(_agent, id, _otherAgentUser.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("AC", "41")]
    public async Task AC41_ChangeStatus_WithoutRowVersion_Returns400()
    {
        var id = await TicketAtAsync("New");

        var response = await _supervisor.PostAsJsonAsync(
            $"/api/Tickets/{id}/status",
            new { status = "Open", rowVersion = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- US-014 — a supervisor assigns -------------------------------------------------------------

    [Fact]
    [Trait("AC", "42")]
    public async Task AC42_Supervisor_AssignsUnassignedTicket_Returns200()
    {
        var id = await CreateTicketAsync();

        var response = await AssignAsync(_supervisor, id, _agentUser.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DetailAsync(_supervisor, id)).AssigneeId.Should().Be(_agentUser.Id);
    }

    [Fact]
    [Trait("AC", "42")]
    public async Task AC42_Supervisor_ReassignsTicket_RecordsReassignedWithPreviousHolder()
    {
        var id = await CreateTicketAsync();
        (await AssignAsync(_supervisor, id, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await AssignAsync(_supervisor, id, _otherAgentUser.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await DetailAsync(_supervisor, id);
        detail.AssigneeId.Should().Be(_otherAgentUser.Id);
        detail.History[0].ChangeType.Should().Be("Reassigned");
        detail.History[0].FromValue.Should().Be(_agentUser.Id.ToString());
        detail.History[0].ToValue.Should().Be(_otherAgentUser.Id.ToString());
    }

    [Fact]
    [Trait("AC", "44")]
    public async Task AC44_Assign_UnknownTargetUser_Returns400KeyedToAssigneeId()
    {
        var id = await CreateTicketAsync();

        var response = await AssignAsync(_supervisor, id, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "AssigneeId");
    }

    /// <summary>
    /// A supervisor is a real user with a real id, so AC-44 cannot be satisfied by an existence
    /// check alone. Without this, the endpoint would happily assign tickets to anyone on the
    /// platform — the knowledge-base editor included.
    /// </summary>
    [Fact]
    [Trait("AC", "44")]
    public async Task AC44_Assign_TargetIsNotAnAgent_Returns400()
    {
        var id = await CreateTicketAsync();
        var (nonAgent, _) = await _factory.CreateUserAsync(Roles.Supervisor);

        var response = await AssignAsync(_supervisor, id, nonAgent.Id);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- US-119 — an agent cannot assign -----------------------------------------------------------

    [Fact]
    [Trait("AC", "43")]
    public async Task AC43_Agent_AssigningAnyTicket_Returns403()
    {
        var id = await CreateTicketAsync();

        var response = await AssignAsync(_agent, id, _otherAgentUser.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The parenthetical in AC-43, and the whole point of US-119. A "reasonable" ownership shortcut
    /// would permit this and would look defensible in review: permission precedes ownership, because
    /// assignment is a supervisory act regardless of who currently holds the ticket.
    /// </summary>
    [Fact]
    [Trait("AC", "43")]
    public async Task AC43_Agent_AssigningTheirOwnTicket_StillReturns403()
    {
        var id = await CreateTicketAsync();
        (await AssignAsync(_supervisor, id, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await AssignAsync(_agent, id, _agentUser.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- US-120 — per-record authorization ----------------------------------------------------------

    [Fact]
    [Trait("AC", "45")]
    public async Task AC45_Agent_ChangingAnotherAgentsTicket_Returns403AndTicketUnchanged()
    {
        var id = await CreateTicketAsync();
        (await AssignAsync(_supervisor, id, _otherAgentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await ChangeStatusAsync(_agent, id, "Open");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // AC-66: ownership refusal carries its documented code, machine-readable rather than prose.
        (await response.Content.ReadFromJsonAsync<Response<object>>())!.Code
            .Should().Be(SystemCode.ERR016);
        (await DetailAsync(_supervisor, id)).Status.Should().Be("New");
    }

    [Fact]
    [Trait("AC", "46")]
    public async Task AC46_Agent_ChangingTheirOwnTicket_Returns200()
    {
        var id = await CreateTicketAsync();
        (await AssignAsync(_supervisor, id, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await ChangeStatusAsync(_agent, id, "Open");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "47")]
    public async Task AC47_Supervisor_ChangingAnyTicket_Returns200()
    {
        var id = await CreateTicketAsync();
        (await AssignAsync(_supervisor, id, _otherAgentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await ChangeStatusAsync(_supervisor, id, "Open");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Not in the story's cases, and added deliberately: an unassigned ticket belongs to nobody, so
    /// an implementation that inverted the check or read null as "anyone" would pass every other
    /// test here and hand every agent every unassigned ticket.
    /// </summary>
    [Fact]
    [Trait("AC", "45")]
    public async Task AC45_Agent_ChangingAnUnassignedTicket_Returns403()
    {
        var id = await CreateTicketAsync();

        var response = await ChangeStatusAsync(_agent, id, "Open");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // Same contract: an unassigned ticket is refused by ownership too, with the same code.
        (await response.Content.ReadFromJsonAsync<Response<object>>())!.Code
            .Should().Be(SystemCode.ERR016);
    }

    // --- US-128's picker (task 0) -------------------------------------------------------------------

    [Fact]
    public async Task AssignableAgents_ReturnsOnlyUsersInTheAgentRole()
    {
        var body = await _supervisor.GetFromJsonAsync<Response<List<AgentOption>>>("/api/Tickets/assignable-agents");

        var ids = body!.Data!.Select(a => a.Id).ToList();
        ids.Should().Contain([_agentUser.Id, _otherAgentUser.Id]);
        body.Data.Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a.Name));
    }

    /// <summary>The picker is a supervisory surface; an agent has no business enumerating staff.</summary>
    [Fact]
    [Trait("AC", "43")]
    public async Task AssignableAgents_AgentIsRefused()
    {
        var response = await _agent.GetAsync("/api/Tickets/assignable-agents");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Day 1 debts, now payable ----------------------------------------------------------------

    /// <summary>
    /// US-035 TC-01, left uncovered in Day 1 because nothing could assign a ticket. AC-34's
    /// positive half: the caller sees their own work and nobody else's.
    /// </summary>
    [Fact]
    [Trait("AC", "34")]
    public async Task AC34_GetTickets_MineReturnsOnlyTicketsAssignedToTheCaller()
    {
        var mine = await CreateTicketAsync();
        var theirs = await CreateTicketAsync();
        (await AssignAsync(_supervisor, mine, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AssignAsync(_supervisor, theirs, _otherAgentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await _agent.GetFromJsonAsync<Response<PagedData<QueueRow>>>("/api/Tickets?mine=true&pageSize=50");

        var ids = page!.Data!.Items.Select(t => t.Id).ToList();
        ids.Should().Contain(mine);
        ids.Should().NotContain(theirs);
    }

    /// <summary>
    /// US-013 TC-02's assignee filter, the last of AC-33's four to become testable.
    /// </summary>
    [Fact]
    [Trait("AC", "33")]
    public async Task AC33_GetTickets_AssigneeFilter_ReturnsOnlyThatAgentsTickets()
    {
        var held = await CreateTicketAsync();
        var other = await CreateTicketAsync();
        (await AssignAsync(_supervisor, held, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AssignAsync(_supervisor, other, _otherAgentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await _supervisor.GetFromJsonAsync<Response<PagedData<QueueRow>>>(
            $"/api/Tickets?assigneeId={_agentUser.Id}&pageSize=50");

        page!.Data!.Items.Should().NotBeEmpty();
        page.Data.Items.Select(t => t.Id).Should().Contain(held).And.NotContain(other);
    }

    // --- MVP-12 — the unassigned filter (AC-82) ----------------------------------------------------

    /// <summary>
    /// AC-82. `assigneeId` is a nullable filter where <em>absent</em> means "any assignee", so
    /// "nobody holds this" is inexpressible without a flag of its own.
    ///
    /// Both directions are asserted deliberately: a filter that quietly returned the whole queue
    /// would satisfy the presence half on its own and look green.
    /// </summary>
    [Fact]
    [Trait("AC", "82")]
    public async Task AC82_GetTickets_Unassigned_ReturnsOnlyTicketsNobodyHolds()
    {
        var nobodys = await CreateTicketAsync();
        var held = await CreateTicketAsync();
        (await AssignAsync(_supervisor, held, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await _supervisor.GetFromJsonAsync<Response<PagedData<QueueRow>>>(
            "/api/Tickets?unassigned=true&pageSize=50");

        var ids = page!.Data!.Items.Select(t => t.Id).ToList();
        ids.Should().Contain(nobodys);
        ids.Should().NotContain(held);
        page.Data.Items.Should().OnlyContain(t => t.AssigneeId == null);
    }

    /// <summary>AC-82 over AC-33: `unassigned` conjoins with the other filters rather than replacing them.</summary>
    [Fact]
    [Trait("AC", "82")]
    public async Task AC82_GetTickets_UnassignedCombinesWithStatus()
    {
        var openAndNobodys = await TicketAtAsync("Open");
        var newAndNobodys = await CreateTicketAsync();
        var openButHeld = await TicketAtAsync("Open");
        (await AssignAsync(_supervisor, openButHeld, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await _supervisor.GetFromJsonAsync<Response<PagedData<QueueRow>>>(
            "/api/Tickets?unassigned=true&status=Open&pageSize=50");

        var ids = page!.Data!.Items.Select(t => t.Id).ToList();
        ids.Should().Contain(openAndNobodys);
        ids.Should().NotContain(newAndNobodys);
        ids.Should().NotContain(openButHeld);
        page.Data.Items.Should().OnlyContain(t => t.Status == "Open" && t.AssigneeId == null);
    }

    /// <summary>
    /// Precedence. `mine` is about the caller and `unassigned` is about nobody, so together they
    /// describe the empty set. Honouring that literally is less useful than honouring the more
    /// specific intent, so `mine` wins — and it is stated in a test rather than left to whichever
    /// `WhereIf` happens to come last.
    /// </summary>
    [Fact]
    [Trait("AC", "82")]
    public async Task AC82_GetTickets_MineWinsOverUnassigned()
    {
        var mine = await CreateTicketAsync();
        var nobodys = await CreateTicketAsync();
        (await AssignAsync(_supervisor, mine, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await _agent.GetFromJsonAsync<Response<PagedData<QueueRow>>>(
            "/api/Tickets?mine=true&unassigned=true&pageSize=50");

        var ids = page!.Data!.Items.Select(t => t.Id).ToList();
        ids.Should().Contain(mine);
        ids.Should().NotContain(nobodys);
        page.Data.Items.Should().OnlyContain(t => t.AssigneeId == _agentUser.Id);
    }

    // --- US-121 / US-022 — history -----------------------------------------------------------------

    [Fact]
    [Trait("AC", "503")]
    public async Task AC503_EveryTicketEvent_PersistsItsOwnHistoryRow()
    {
        var id = await CreateTicketAsync();
        (await AssignAsync(_supervisor, id, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AssignAsync(_supervisor, id, _otherAgentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ChangeStatusAsync(_supervisor, id, "Open")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ChangeStatusAsync(_supervisor, id, "Assigned")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ChangeStatusAsync(_supervisor, id, "In Progress")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ChangeStatusAsync(_supervisor, id, "Resolved")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ChangeStatusAsync(_supervisor, id, "In Progress")).StatusCode.Should().Be(HttpStatusCode.OK);

        var history = (await DetailAsync(_supervisor, id)).History;

        history.Select(h => h.ChangeType).Should().Equal(
            "Reopened", "StatusChanged", "StatusChanged", "StatusChanged", "StatusChanged", "Reassigned", "Assigned", "Created");

        foreach (var entry in history)
        {
            entry.ActorId.Should().NotBeEmpty();
            entry.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        }

        var reopened = history[0];
        reopened.FromValue.Should().Be("Resolved");
        reopened.ToValue.Should().Be("In Progress");
    }

    [Fact]
    [Trait("AC", "50")]
    public async Task AC50_TicketHistory_IsNewestFirstWithActorDisplayNames()
    {
        var id = await CreateTicketAsync();
        (await AssignAsync(_supervisor, id, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var history = (await DetailAsync(_supervisor, id)).History;

        history.Should().BeInDescendingOrder(h => h.OccurredAt);
        history.Should().OnlyContain(h => !string.IsNullOrWhiteSpace(h.ActorName));
    }

    /// <summary>
    /// AC-50's second half. Denormalising the name into the row would render without a lookup — and
    /// would freeze a name that changes, inside an append-only table that by construction can never
    /// be corrected.
    /// </summary>
    [Fact]
    [Trait("AC", "50")]
    public async Task AC50_HistoryRow_StoresActorIdNotName()
    {
        var id = await CreateTicketAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.TicketHistory.AsNoTracking().FirstAsync(h => h.TicketId == id);

        row.ActorId.Should().NotBeEmpty();
        db.Entry(row).Properties.Select(p => p.Metadata.Name)
            .Should().NotContain(name => name.Contains("ActorName", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// ADR-0010's central claim, finally under test. The ADR traded absent columns for a
    /// SaveChanges guard partly *because the guard is testable* — this is the test that
    /// substantiates it, or the ADR needs rewriting.
    /// </summary>
    [Fact]
    [Trait("AC", "49")]
    public async Task AC49_UpdatingAHistoryRow_IsRefused()
    {
        var id = await CreateTicketAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.TicketHistory.FirstAsync(h => h.TicketId == id);

        db.Entry(row).Property(h => h.ToValue).CurrentValue = "Tampered";

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*");
    }

    [Fact]
    [Trait("AC", "49")]
    public async Task AC49_DeletingAHistoryRow_IsRefused()
    {
        var id = await CreateTicketAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.TicketHistory.FirstAsync(h => h.TicketId == id);

        db.TicketHistory.Remove(row);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*");
    }

    /// <summary>
    /// A test about what does NOT exist. US-121 TC-02 asks for a surface audit, which no ordinary
    /// endpoint test can express — and what it guards against is a future HistoryController that
    /// nobody reviews carefully.
    /// </summary>
    [Fact]
    [Trait("AC", "49")]
    public void AC49_NoEndpointExposesHistoryMutation()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>();

        var offenders = new List<string>();

        foreach (var endpoint in endpoints)
        {
            var pattern = endpoint.RoutePattern.RawText ?? string.Empty;
            if (!pattern.Contains("history", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                          ?? [];

            foreach (var method in methods.Where(m =>
                         m is "PUT" or "PATCH" or "DELETE" or "POST"))
            {
                offenders.Add($"{method} {pattern}");
            }
        }

        offenders.Should().BeEmpty(
            "history is append-only (AC-49); no route may mutate it");
    }

    // --- wire shapes -------------------------------------------------------------------------------

    public sealed record TicketDetail(
        Guid Id,
        string Reference,
        string Subject,
        string Status,
        string Priority,
        Guid? AssigneeId,
        string RowVersion,
        CustomerSummary Customer,
        string CategoryName,
        List<HistoryEntry> History);

    public sealed record CustomerSummary(Guid Id, string Name, string Email, string? Phone);

    public sealed record HistoryEntry(
        Guid Id,
        string ChangeType,
        string? FromValue,
        string? ToValue,
        Guid ActorId,
        string ActorName,
        DateTime OccurredAt);

    public sealed record AgentOption(Guid Id, string Name, string Email);

    public sealed record QueueRow(Guid Id, string Reference, string Status, Guid? AssigneeId);
}
