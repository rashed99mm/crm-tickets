using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-16 — departments and branches. `AC-115` through `AC-120`, `AC-123`.
/// Real LocalDB, same reasoning as every other endpoint suite here.
/// </summary>
public class OrganisationStructureEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _agent = null!;
    private Guid _agentUserId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        (_agent, var agent) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        _agentUserId = agent.Id;
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        _agent.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    // --- AC-118 — the seed -------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "118")]
    public async Task AC118_DefaultDepartmentAndBranch_AreSeededAndActive()
    {
        var department = await _admin.GetFromJsonAsync<Response<DepartmentRow>>(
            "/api/Departments/00000000-0000-0000-0000-000000000001");
        var branch = await _admin.GetFromJsonAsync<Response<BranchRow>>(
            "/api/Branches/00000000-0000-0000-0000-000000000001");

        department!.Data!.Name.Should().Be("General");
        department.Data.IsActive.Should().BeTrue();

        branch!.Data!.Name.Should().Be("Head Office");
        branch.Data.Timezone.Should().Be("UTC");
        branch.Data.IsActive.Should().BeTrue();
    }

    // --- AC-119 — department CRUD ---------------------------------------------------------------

    [Fact]
    [Trait("AC", "119")]
    public async Task AC119_Department_FullCrudCycle()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var create = await _admin.PostAsJsonAsync("/api/Departments", new { name = $"Support-{suffix}", managerId = (Guid?)null });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.Location.Should().NotBeNull();
        var id = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var update = await _admin.PutAsJsonAsync($"/api/Departments/{id}", new { name = $"Customer Support-{suffix}", managerId = (Guid?)null });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await _admin.GetFromJsonAsync<Response<DepartmentRow>>($"/api/Departments/{id}");
        read!.Data!.Name.Should().Be($"Customer Support-{suffix}");

        var delete = await _admin.DeleteAsync($"/api/Departments/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterDelete = await _admin.GetFromJsonAsync<Response<DepartmentRow>>($"/api/Departments/{id}");
        afterDelete!.Data!.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "119")]
    public async Task AC119_GetDepartments_ReturnsAPagedList()
    {
        var response = await _admin.GetFromJsonAsync<Response<PagedRow<DepartmentRow>>>("/api/Departments");

        response!.Data!.Items.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("AC", "119")]
    public async Task AC119_UnknownDepartmentId_Returns404()
    {
        var response = await _admin.GetAsync($"/api/Departments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- AC-120 — non-admin refused --------------------------------------------------------------

    [Fact]
    [Trait("AC", "120")]
    public async Task AC120_NonAdmin_CannotCreateDepartment()
    {
        var response = await _agent.PostAsJsonAsync(
            "/api/Departments", new { name = $"Rogue-{Guid.NewGuid():N}", managerId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("AC", "120")]
    public async Task AC120_NonAdmin_CannotCreateBranch()
    {
        var response = await _agent.PostAsJsonAsync(
            "/api/Branches", new { name = $"Rogue Branch-{Guid.NewGuid():N}", region = (string?)null, timezone = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("AC", "120")]
    public async Task AC120_NonAdmin_CanStillReadDepartments()
    {
        // Reads need only a session, not the Admin role — the mutation gate is on writes.
        var response = await _agent.GetAsync("/api/Departments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- AC-17 — branch-scoped reads -------------------------------------------------------------

    [Fact]
    [Trait("AC", "17")]
    public async Task AC17_BranchUser_SeesOnlyTicketsAndCustomersInOwnBranch()
    {
        var branch = Guid.Parse("00000000-0000-0000-0000-000000000001");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(_agentUserId);
            user!.AssignOrganisation(null, branch, null);
            await db.SaveChangesAsync();
        }

        var category = (await _admin.GetFromJsonAsync<Response<IReadOnlyList<CategoryRow>>>("/api/Categories"))!
            .Data!.First();

        var ownedCustomer = await CreateCustomerAsync(_agent, "branch-owned");
        var unscopedCustomer = await CreateCustomerAsync(_admin, "branch-unscoped");

        var ownedTicket = await CreateTicketAsync(_admin, ownedCustomer, category.Id);
        var unscopedTicket = await CreateTicketAsync(_admin, unscopedCustomer, category.Id);

        var assign = await _admin.PostAsJsonAsync($"/api/Tickets/{ownedTicket}/assignee", new
        {
            assigneeId = _agentUserId,
            rowVersion = (await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ownedTicket}"))!.Data!.RowVersion
        });
        assign.StatusCode.Should().Be(HttpStatusCode.OK);

        var tickets = await _agent.GetFromJsonAsync<Response<PagedRow<TicketRow>>>("/api/Tickets?pageSize=50");
        tickets!.Data!.Items.Select(x => x.Id).Should().Contain(ownedTicket).And.NotContain(unscopedTicket);

        var customers = await _agent.GetFromJsonAsync<Response<PagedRow<CustomerRow>>>("/api/Customers?pageSize=50");
        customers!.Data!.Items.Select(x => x.Id).Should().Contain(ownedCustomer).And.NotContain(unscopedCustomer);

        (await _agent.GetAsync($"/api/Tickets/{ownedTicket}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _agent.GetAsync($"/api/Tickets/{unscopedTicket}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- AC-121 — validation --------------------------------------------------------------------

    [Fact]
    [Trait("AC", "121")]
    public async Task AC121_CreateDepartment_EmptyName_Returns400KeyedToName()
    {
        var response = await _admin.PostAsJsonAsync(
            "/api/Departments", new { name = "   ", managerId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "Name");
    }

    [Fact]
    [Trait("AC", "121")]
    public async Task AC121_CreateDepartment_DuplicateName_Returns409()
    {
        var name = $"Duplicate-{Guid.NewGuid():N}";
        (await _admin.PostAsJsonAsync("/api/Departments", new { name, managerId = (Guid?)null }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _admin.PostAsJsonAsync("/api/Departments", new { name, managerId = (Guid?)null });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // --- AC-122 — unknown id on update/delete ------------------------------------------------------

    [Fact]
    [Trait("AC", "122")]
    public async Task AC122_UpdateUnknownDepartment_Returns404()
    {
        var response = await _admin.PutAsJsonAsync(
            $"/api/Departments/{Guid.NewGuid()}", new { name = "Ghost", managerId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "122")]
    public async Task AC122_DeleteUnknownBranch_Returns404()
    {
        var response = await _admin.DeleteAsync($"/api/Branches/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- AC-123 — branch CRUD ----------------------------------------------------------------------

    [Fact]
    [Trait("AC", "123")]
    public async Task AC123_Branch_FullCrudCycle()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var create = await _admin.PostAsJsonAsync("/api/Branches", new { name = $"Riyadh-{suffix}", region = "KSA", timezone = "Asia/Riyadh" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var read = await _admin.GetFromJsonAsync<Response<BranchRow>>($"/api/Branches/{id}");
        read!.Data!.Region.Should().Be("KSA");
        read.Data.Timezone.Should().Be("Asia/Riyadh");

        var update = await _admin.PutAsJsonAsync($"/api/Branches/{id}", new { name = $"Riyadh HQ-{suffix}", region = "KSA", timezone = "Asia/Riyadh" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await _admin.DeleteAsync($"/api/Branches/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "123")]
    public async Task AC123_CreateBranch_NoTimezoneGiven_DefaultsToUtc()
    {
        var create = await _admin.PostAsJsonAsync(
            "/api/Branches", new { name = $"Cairo-{Guid.NewGuid():N}", region = (string?)null, timezone = (string?)null });
        var id = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var read = await _admin.GetFromJsonAsync<Response<BranchRow>>($"/api/Branches/{id}");
        read!.Data!.Timezone.Should().Be("UTC");
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient client, string prefix)
    {
        var response = await client.PostAsJsonAsync("/api/Customers", new
        {
            name = $"{prefix}-{Guid.NewGuid():N}",
            email = $"{prefix}-{Guid.NewGuid():N}@example.com"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    private static async Task<Guid> CreateTicketAsync(HttpClient client, Guid customerId, Guid categoryId)
    {
        var response = await client.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Branch scope test",
            description = "Created by the organisation scoping integration test.",
            customerId,
            categoryId,
            priority = "Normal"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    public sealed record DepartmentRow(Guid Id, string Name, Guid? ManagerId, bool IsActive, DateTime CreatedAt);
    public sealed record BranchRow(Guid Id, string Name, string? Region, string Timezone, bool IsActive, DateTime CreatedAt);
    public sealed record CategoryRow(Guid Id, string Name);
    public sealed record CustomerRow(Guid Id, string Name, string Email, string? Phone, DateTime CreatedAt);
    public sealed record TicketRow(Guid Id, string RowVersion);
    public sealed record PagedRow<T>(IReadOnlyList<T> Items, int PageIndex, int PageSize, int TotalCount);
}
