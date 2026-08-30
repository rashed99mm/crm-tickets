using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// `MVP-02` — administering staff accounts and roles, against a real database.
///
/// An **acceptance pass** over inherited code: `/api/Users` and the Angular staff screen already
/// shipped, but no test named `MVP-02`'s five criteria, so none of them was proven. These tests are
/// written from the criteria rather than from the handlers, and where the two disagree it is the
/// handler that moves.
///
/// Over LocalDB rather than in memory for the same reason as the rest of the CRM suite: role
/// membership, the `IsActive` flag and the history foreign keys are all things the real provider
/// enforces and the in-memory one does not.
/// </summary>
public class StaffAdministrationTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;

    /// <summary>
    /// The login endpoint is rate limited to five attempts per five minutes per caller, so each
    /// test keeps its sign-ins countable. A factory — and therefore a host, and therefore a fresh
    /// limiter — is built per test, which is what keeps that budget per test rather than per class.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private const string StaffPassword = "Staff-Password-742";

    // --- fixtures --------------------------------------------------------------------------------

    private sealed record Staff(Guid Id, string Email, string FirstName, string LastName)
    {
        public string FullName => $"{FirstName} {LastName}";
    }

    /// <summary>
    /// Creates a staff account through the administration endpoint itself — not through
    /// <see cref="CrmApiFactory.CreateUserAsync"/>. The criteria are about what an administrator can
    /// do over the wire, and a fixture that went straight to <c>UserManager</c> would prove nothing
    /// about the endpoint.
    /// </summary>
    private async Task<Staff> CreateStaffAsync(string role, string firstName = "Nadia", string lastName = "Fahmy")
    {
        var email = $"staff-{Guid.NewGuid():N}@test.local";

        var response = await _admin.PostAsJsonAsync("/api/Users", new
        {
            email,
            username = email,
            password = StaffPassword,
            firstName,
            lastName,
            phoneNumber = "+20 100 000 0000",
            roles = new[] { role },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<Response<Guid>>();

        return new Staff(created!.Data!, email, firstName, lastName);
    }

    private async Task<UserRow> GetStaffAsync(Guid id)
    {
        var body = await _admin.GetFromJsonAsync<Response<UserRow>>($"/api/Users/{id}");
        return body!.Data!;
    }

    private Task<HttpResponseMessage> DeactivateAsync(Guid id) =>
        _admin.PutAsync($"/api/Users/{id}/deactivate", content: null);

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/Auth/login", new { email, password });

    // --- criterion 1 — an administrator creates staff with a support role -------------------------

    /// <summary>
    /// The role has to survive the round trip, not merely be accepted: a handler that ignored the
    /// <c>roles</c> array would answer 201 just as happily, so the assertion is on what the read
    /// side reports afterwards.
    /// </summary>
    [Fact]
    [Trait("MVP", "02")]
    public async Task MVP02_Admin_CreatesStaffWithAnAgentRole()
    {
        var staff = await CreateStaffAsync(ApplicationRole.Roles.Agent, "Karim", "Saleh");

        var row = await GetStaffAsync(staff.Id);

        row.Email.Should().Be(staff.Email);
        row.Roles.Should().Contain(ApplicationRole.Roles.Agent);
        row.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("MVP", "02")]
    public async Task MVP02_Admin_CreatesStaffWithASupervisorRole()
    {
        var staff = await CreateStaffAsync(ApplicationRole.Roles.Supervisor, "Hala", "Mansour");

        var row = await GetStaffAsync(staff.Id);

        row.Roles.Should().Contain(ApplicationRole.Roles.Supervisor);
        row.IsActive.Should().BeTrue();
    }

    // --- criterion 3 — the staff surface refuses everyone else ------------------------------------

    /// <summary>
    /// A **supervisor**, deliberately: the near miss is the case worth testing. ADR-0012 is explicit
    /// that "administers the platform" and "hands work out" are different claims, so the most senior
    /// support role in the product is still refused the user-administration surface.
    ///
    /// Reads and writes both, because an endpoint list is only as protected as its least protected
    /// verb.
    /// </summary>
    [Fact]
    [Trait("MVP", "02")]
    public async Task MVP02_NonAdmin_IsRefusedTheStaffSurface()
    {
        var (supervisor, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Supervisor);
        using var scoped = supervisor;

        var subject = await CreateStaffAsync(ApplicationRole.Roles.Agent);

        (await supervisor.GetAsync("/api/Users")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await supervisor.GetAsync($"/api/Users/{subject.Id}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        var create = await supervisor.PostAsJsonAsync("/api/Users", new
        {
            email = $"smuggled-{Guid.NewGuid():N}@test.local",
            username = $"smuggled-{Guid.NewGuid():N}",
            password = StaffPassword,
            firstName = "Not",
            lastName = "Allowed",
            roles = new[] { ApplicationRole.Roles.Agent },
        });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await supervisor.PutAsync($"/api/Users/{subject.Id}/deactivate", content: null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await supervisor.PutAsJsonAsync($"/api/Users/{subject.Id}/roles",
            new { roles = new[] { ApplicationRole.Roles.Supervisor } })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        // The refusal has to be real rather than cosmetic: the subject is unchanged afterwards.
        var after = await GetStaffAsync(subject.Id);
        after.IsActive.Should().BeTrue();
        after.Roles.Should().Contain(ApplicationRole.Roles.Agent);
    }

    // --- criterion 2 — a deactivated employee cannot sign in --------------------------------------

    /// <summary>
    /// The criterion this pass exists for. <c>ApplicationUser.IsActive</c> is this platform's own
    /// flag, and ASP.NET Identity's sign-in path knows nothing about it — it checks
    /// <c>LockoutEnabled</c>/<c>LockoutEnd</c>. Unless the login handler tests <c>IsActive</c>
    /// itself, a deactivated employee keeps working credentials.
    ///
    /// The first sign-in is not ceremony: without it, a test that only ever saw a refusal could not
    /// tell "deactivation worked" from "these credentials never worked".
    /// </summary>
    [Fact]
    [Trait("MVP", "02")]
    public async Task MVP02_DeactivatedStaff_CannotSignIn()
    {
        var staff = await CreateStaffAsync(ApplicationRole.Roles.Agent);

        using var client = _factory.CreateClient();

        var before = await SignInAsync(client, staff.Email, StaffPassword);
        before.StatusCode.Should().Be(HttpStatusCode.OK, "the credentials must work before they are revoked");
        var session = (await before.Content.ReadFromJsonAsync<Response<CrmApiFactory.LoginData>>())!.Data!;
        session.AccessToken.Should().NotBeNullOrWhiteSpace();

        (await DeactivateAsync(staff.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetStaffAsync(staff.Id)).IsActive.Should().BeFalse();

        var after = await SignInAsync(client, staff.Email, StaffPassword);

        after.IsSuccessStatusCode.Should().BeFalse("a deactivated employee must not be able to sign in");

        // Asserted on the raw body rather than a typed model: the criterion is that no credential is
        // handed out, and a deserialiser that quietly dropped an unexpected field would hide exactly
        // the failure this is looking for.
        var body = await after.Content.ReadAsStringAsync();
        body.Should().NotContainEquivalentOf("accessToken");
        body.Should().Contain(SystemCode.ERR026);

        // The refresh token issued before the deactivation is the way back in that a password check
        // never sees: refusing the password while honouring the token would leave the employee
        // renewing their session indefinitely.
        var refresh = await client.PostAsJsonAsync("/api/Auth/refresh", new
        {
            accessToken = session.AccessToken,
            refreshToken = session.RefreshToken,
        });

        refresh.IsSuccessStatusCode.Should().BeFalse(
            "the session a deactivated employee already held must not be renewable");
        (await refresh.Content.ReadAsStringAsync()).Should().NotContainEquivalentOf("accessToken");
    }

    /// <summary>
    /// The other half of criterion 2, and the reason staff are deactivated rather than deleted: a
    /// deleted agent takes the authorship of every history row with them.
    ///
    /// The history entry is written by the agent themselves — created before deactivation, read
    /// after it — so the row's actor is genuinely a disabled account and not the caller.
    /// </summary>
    [Fact]
    [Trait("MVP", "02")]
    public async Task MVP02_DeactivatedStaff_KeepTheirHistory()
    {
        var staff = await CreateStaffAsync(ApplicationRole.Roles.Agent, "Yusra", "Barakat");

        using var staffClient = _factory.CreateClient();
        var signIn = await SignInAsync(staffClient, staff.Email, StaffPassword);
        signIn.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await signIn.Content.ReadFromJsonAsync<Response<CrmApiFactory.LoginData>>())!.Data!.AccessToken;
        staffClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var customerId = await CreateCustomerAsync(staffClient);
        var categoryId = await _factory.EnsureCategoryAsync("Technical");

        var create = await staffClient.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot reach the portal",
            description = "The sign-in page times out.",
            customerId,
            categoryId,
            priority = "Normal",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticketId = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;

        (await DeactivateAsync(staff.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = (await _admin.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}"))!.Data!;

        var authored = detail.History.Should().ContainSingle(h => h.ActorId == staff.Id).Subject;
        authored.ActorName.Should().Be(staff.FullName,
            "a deactivated agent's history rows must still resolve the person who wrote them");
        authored.ChangeType.Should().Be("Created");
    }

    // --- criterion 4 — a deactivated agent is not offered as an assignee --------------------------

    /// <summary>
    /// <c>GetUsersInRoleAsync</c> filters on <c>IsActive</c>, so this is expected to hold — but
    /// nothing tested it, which means nothing would notice if the filter were dropped. The
    /// before/after pair is what makes it a test of the filter rather than of the fixture.
    /// </summary>
    [Fact]
    [Trait("MVP", "02")]
    public async Task MVP02_DeactivatedAgent_IsNotOfferedAsAnAssignee()
    {
        var staff = await CreateStaffAsync(ApplicationRole.Roles.Agent, "Omar", "Rashid");

        // The Admin policy satisfies the Supervisor policy (ADR-0012), so the administrator can read
        // the picker without a second sign-in against the login rate limit.
        var before = (await _admin.GetFromJsonAsync<Response<List<AgentOption>>>("/api/Tickets/assignable-agents"))!.Data!;
        before.Should().Contain(a => a.Id == staff.Id);

        (await DeactivateAsync(staff.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = (await _admin.GetFromJsonAsync<Response<List<AgentOption>>>("/api/Tickets/assignable-agents"))!.Data!;
        after.Should().NotContain(a => a.Id == staff.Id,
            "work cannot be handed to someone who can no longer sign in to do it");
    }

    /// <summary>
    /// The other half of criterion 4, and the half the picker cannot enforce. Removing someone from
    /// a dropdown is a *presentation* fix: the mutation the dropdown feeds is a separate decision,
    /// and a supervisor holding a page rendered before the deactivation — or anything calling the
    /// API directly — still names the id.
    ///
    /// "No longer offered as an assignee" is a claim about who can be handed work, so the endpoint
    /// has to refuse it too, keyed to <c>AssigneeId</c> like every other refusal of that field.
    /// </summary>
    [Fact]
    [Trait("MVP", "02")]
    public async Task MVP02_DeactivatedAgent_CannotBeHandedWorkDirectly()
    {
        var staff = await CreateStaffAsync(ApplicationRole.Roles.Agent, "Tarek", "Halim");

        var customerId = await CreateCustomerAsync(_admin);
        var categoryId = await _factory.EnsureCategoryAsync("Technical");

        var create = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Printer offline",
            description = "The office printer stopped responding.",
            customerId,
            categoryId,
            priority = "Normal",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticketId = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;

        (await DeactivateAsync(staff.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var rowVersion = (await _admin.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}"))!
            .Data!.RowVersion;

        var assign = await _admin.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee", new
        {
            assigneeId = staff.Id,
            rowVersion,
        });

        assign.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "work cannot be handed to an account that can no longer sign in to do it");

        var error = await assign.Content.ReadFromJsonAsync<Response<object>>();
        error!.Errors.Should().Contain(e => e.Field == "AssigneeId");

        // And the refusal has to have actually held: an error body over a completed write is worse
        // than either.
        (await _admin.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}"))!
            .Data!.AssigneeId.Should().BeNull();
    }

    // --- criterion 6 — the users list filters by role server-side ---------------------------------

    /// <summary>
    /// The staff screen paginates server-side, so "every agent" can never be a client-side filter —
    /// a client can only see the page it fetched. A `role` parameter must narrow the whole result
    /// set. The control half of the test proves both newly-created accounts match the search term,
    /// so it is the role filter, not the search, that excludes the `User` account from the second call.
    /// </summary>
    [Fact]
    [Trait("MVP", "02")]
    public async Task MVP02_UsersList_CanBeNarrowedToOneRole()
    {
        var tag = $"role-{Guid.NewGuid():N}";
        var agent = await CreateStaffAsync(ApplicationRole.Roles.Agent, tag);
        var user = await CreateStaffAsync(ApplicationRole.Roles.User, tag);

        var all = await _admin.GetAsync($"/api/Users?page=1&pageSize=50&search={tag}");
        var allBody = await all.Content.ReadAsStringAsync();
        all.StatusCode.Should().Be(HttpStatusCode.OK, allBody);
        (await all.Content.ReadFromJsonAsync<Response<UserPage>>())!.Data!.Items
            .Select(row => row.Id)
            .Should().Contain(new[] { agent.Id, user.Id });

        var response = await _admin.GetAsync($"/api/Users?page=1&pageSize=50&search={tag}&role={ApplicationRole.Roles.Agent}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = (await response.Content.ReadFromJsonAsync<Response<UserPage>>())!.Data!.Items;

        rows.Should().OnlyContain(row => row.Roles.Contains(ApplicationRole.Roles.Agent));
        rows.Select(row => row.Id).Should().NotContain(user.Id);
        rows.Select(row => row.Id).Should().Contain(agent.Id);
    }

    // --- criterion 5 — no response carries a password or its hash ---------------------------------

    /// <summary>
    /// Swept over the **raw JSON**, deliberately. Reading into <c>UserDto</c> and finding no password
    /// would only prove that <c>UserDto</c> has no password property; it says nothing about what the
    /// serialiser actually put on the wire.
    ///
    /// The sent password itself is in the sweep because the likeliest leak is not a hash but an echo
    /// — a create endpoint returning the request it was given.
    /// </summary>
    [Fact]
    [Trait("MVP", "02")]
    public async Task MVP02_NoResponseCarriesAPasswordOrHash()
    {
        var email = $"staff-{Guid.NewGuid():N}@test.local";

        var create = await _admin.PostAsJsonAsync("/api/Users", new
        {
            email,
            username = email,
            password = StaffPassword,
            firstName = "Salma",
            lastName = "Adel",
            roles = new[] { ApplicationRole.Roles.Agent },
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;

        var bodies = new Dictionary<string, string>
        {
            ["create"] = await create.Content.ReadAsStringAsync(),
            ["list"] = await (await _admin.GetAsync($"/api/Users?page=1&pageSize=50&search={email}"))
                .Content.ReadAsStringAsync(),
            ["detail"] = await (await _admin.GetAsync($"/api/Users/{id}")).Content.ReadAsStringAsync(),
        };

        string[] forbidden = ["password", "passwordHash", "securityStamp", StaffPassword];

        foreach (var (name, body) in bodies)
        {
            body.Should().NotBeNullOrWhiteSpace();

            foreach (var needle in forbidden)
            {
                // Case-insensitive by design: `passwordHash`, `PasswordHash` and `passwordhash` are
                // the same leak, and which one appears depends on a serialiser setting.
                body.Should().NotContainEquivalentOf(needle,
                    "the {0} response must not carry '{1}'", name, needle);
            }
        }

        // The rejection path, swept for the submitted VALUE only. Identity's own complaints
        // legitimately contain the word "password" ("Passwords must be at least..."), so sweeping
        // the word here would only teach the next reader to weaken the assertion; the leak that
        // matters is a handler echoing back what it was sent.
        const string weak = "abc";
        var rejected = await _admin.PostAsJsonAsync("/api/Users", new
        {
            email = $"weak-{Guid.NewGuid():N}@test.local",
            username = $"weak-{Guid.NewGuid():N}",
            password = weak,
            firstName = "Too",
            lastName = "Short",
            roles = new[] { ApplicationRole.Roles.Agent },
        });

        rejected.IsSuccessStatusCode.Should().BeFalse("a password below the policy must not create an account");
        (await rejected.Content.ReadAsStringAsync()).Should().NotContain($"\"{weak}\"",
            "a refusal must not echo the credential it refused");
    }

    // --- helpers ---------------------------------------------------------------------------------

    private static async Task<Guid> CreateCustomerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Layla Haddad",
            email = $"staffadmin-{Guid.NewGuid():N}@example.com",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    private sealed record UserRow(
        Guid Id,
        string Email,
        string Username,
        string FirstName,
        string LastName,
        bool IsActive,
        List<string> Roles);

    private sealed record UserPage(
        List<UserRow> Items,
        int PageIndex,
        int PageSize,
        int TotalCount);

    private sealed record TicketDetailRow(
        Guid Id,
        string Reference,
        Guid? AssigneeId,
        string RowVersion,
        List<HistoryRow> History);

    private sealed record HistoryRow(
        Guid Id,
        string ChangeType,
        string? FromValue,
        string? ToValue,
        Guid ActorId,
        string ActorName,
        DateTime OccurredAt);

    private sealed record AgentOption(Guid Id, string Name, string Email);
}
