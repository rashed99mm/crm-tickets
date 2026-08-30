using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-21 (`US-801`) — the audit log query endpoint, and the `AuditBehavior` fix that makes it
/// return real data (`AC-144`/`AC-145`). Real LocalDB throughout.
/// </summary>
public class AuditLogEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _agent = null!;
    private Guid _adminUserId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        ApplicationUser adminUser;
        (_admin, adminUser) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        _adminUserId = adminUser.Id;
        (_agent, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        _agent.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private Task<HttpResponseMessage> CreateStaffAsync(string username) =>
        _admin.PostAsJsonAsync("/api/Users", new
        {
            email = $"{username}@example.com",
            username,
            password = "Created-Password-1",
            firstName = "Audit",
            lastName = "Fixture",
            phoneNumber = (string?)null,
            roles = new[] { "User" },
        });

    // --- AC-144/145 — AuditBehavior actually writes entries now ------------------------------------

    [Fact]
    [Trait("AC", "144")]
    public async Task AC144_SuccessfulAuditableCommand_WritesAnAuditLogRow()
    {
        var username = $"auditee-{Guid.NewGuid():N}";
        var create = await CreateStaffAsync(username);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var userId = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var entries = await GetAuditLogAsync(actionType: "Created", userId: null);

        entries.Should().Contain(e => e.EntityType == "User" && e.EntityId == userId && e.Action == "Created");
    }

    [Fact]
    [Trait("AC", "145")]
    public async Task AC145_FailedAuditableCommand_WritesNoAuditLogRow()
    {
        var before = (await GetAuditLogAsync(null, null)).Count;

        // Missing required fields — fails validation before the handler ever runs.
        var response = await _admin.PostAsJsonAsync("/api/Users", new
        {
            email = "",
            username = "",
            password = "",
            firstName = "",
            lastName = "",
            phoneNumber = (string?)null,
            roles = Array.Empty<string>(),
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var after = (await GetAuditLogAsync(null, null)).Count;

        // A validation failure adds nothing — a strict equality on total count would also catch an
        // unrelated concurrent write, which the "no row for THIS attempt" claim doesn't need to.
        after.Should().Be(before);
    }

    // --- AC-140/141/142 — the query endpoint itself -----------------------------------------------

    [Fact]
    [Trait("AC", "140")]
    public async Task AC140_GetAuditLog_ReturnsNewestFirst()
    {
        await CreateStaffAsync($"first-{Guid.NewGuid():N}");
        await Task.Delay(20);
        await CreateStaffAsync($"second-{Guid.NewGuid():N}");

        var response = await _admin.GetFromJsonAsync<Response<PagedRow<AuditLogRow>>>(
            "/api/admin/audit-log?pageSize=5");

        response!.Data!.Items.Should().BeInDescendingOrder(e => e.CreatedAt);
    }

    [Fact]
    [Trait("AC", "141")]
    public async Task AC141_FilterByActionType_ReturnsOnlyThatAction()
    {
        // A fixture of its own — the filter must exclude non-"Created" rows even when both kinds
        // exist, not merely happen to find only "Created" rows because nothing else ran first.
        var create = await CreateStaffAsync($"filter-action-{Guid.NewGuid():N}");
        var userId = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
        (await _admin.PutAsJsonAsync($"/api/Users/{userId}", new
        {
            firstName = "Renamed",
            lastName = "Fixture",
            phoneNumber = (string?)null,
            profileImageUrl = (string?)null,
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _admin.GetFromJsonAsync<Response<PagedRow<AuditLogRow>>>(
            "/api/admin/audit-log?actionType=Created&pageSize=100");

        response!.Data!.Items.Should().Contain(e => e.EntityId == userId && e.Action == "Created");
        response.Data.Items.Should().OnlyContain(e => e.Action == "Created");
    }

    [Fact]
    [Trait("AC", "142")]
    public async Task AC142_FilterByUserId_ReturnsOnlyThatUsersEntries()
    {
        // AuditLog.UserId is the ACTOR (who did it), not the entity that was acted upon — this
        // admin, in every fixture in this file.
        var create = await CreateStaffAsync($"filtered-{Guid.NewGuid():N}");
        var entityId = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var response = await _admin.GetFromJsonAsync<Response<PagedRow<AuditLogRow>>>(
            $"/api/admin/audit-log?userId={_adminUserId}&pageSize=100");

        response!.Data!.Items.Should().Contain(e => e.EntityId == entityId);
        response.Data.Items.Should().OnlyContain(e => e.UserId == _adminUserId);
    }

    [Fact]
    [Trait("AC", "143")]
    public async Task AC143_NonAdmin_CannotReadAuditLog()
    {
        var response = await _agent.GetAsync("/api/admin/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<List<AuditLogRow>> GetAuditLogAsync(string? actionType, Guid? userId)
    {
        var query = new List<string>();
        if (actionType is not null) query.Add($"actionType={actionType}");
        if (userId is not null) query.Add($"userId={userId}");
        query.Add("pageSize=100");

        var response = await _admin.GetFromJsonAsync<Response<PagedRow<AuditLogRow>>>(
            $"/api/admin/audit-log?{string.Join('&', query)}");

        return [.. response!.Data!.Items];
    }

    public sealed record AuditLogRow(
        Guid Id, Guid UserId, string? UserName, string Action, string EntityType, Guid EntityId,
        string? OldValues, string? NewValues, string? IpAddress, string? UserAgent, DateTime CreatedAt);

    public sealed record PagedRow<T>(IReadOnlyList<T> Items, int PageIndex, int PageSize, int TotalCount);
}
