using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

public class BusinessHoursCalendarEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;

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

    private async Task<Guid> CreateBranchAsync()
    {
        var response = await _admin.PostAsJsonAsync("/api/Branches", new
        {
            name = $"Branch {Guid.NewGuid():N}",
            region = "Riyadh",
            timezone = "Asia/Riyadh",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    [Fact]
    [Trait("AC", "228")]
    public async Task AC228_CreateCalendarRow_Returns201()
    {
        var branchId = await CreateBranchAsync();

        var response = await _admin.PostAsJsonAsync("/api/BusinessHours/calendars", new
        {
            branchId,
            dayOfWeek = "Monday",
            openTime = "09:00",
            closeTime = "17:00",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("AC", "228")]
    public async Task AC228_CreateHoliday_Returns201()
    {
        var branchId = await CreateBranchAsync();

        var response = await _admin.PostAsJsonAsync("/api/BusinessHours/holidays", new
        {
            branchId,
            holidayDate = "2026-12-25",
            name = "Public holiday",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("AC", "228")]
    public async Task AC228_ListCalendars_ReturnsCreatedRow()
    {
        var branchId = await CreateBranchAsync();
        await _admin.PostAsJsonAsync("/api/BusinessHours/calendars", new
        {
            branchId, dayOfWeek = "Tuesday", openTime = "09:00", closeTime = "17:00",
        });

        var list = await _admin.GetFromJsonAsync<Response<PagedData<CalendarRow>>>(
            "/api/BusinessHours/calendars?pageSize=50");

        list!.Data!.Items.Should().Contain(r => r.DayOfWeek == "Tuesday");
    }

    private sealed record CalendarRow(Guid Id, Guid BranchId, string DayOfWeek, string OpenTime, string CloseTime);
}
