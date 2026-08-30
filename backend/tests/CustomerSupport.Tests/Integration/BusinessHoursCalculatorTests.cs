using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

public class BusinessHoursCalculatorTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private Guid _branchId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _branchId = Guid.NewGuid();
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private IBusinessHoursCalculator Calculator
        => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IBusinessHoursCalculator>();

    [Fact]
    [Trait("AC", "225")]
    public async Task AC225_SkipsNonWorkingTime()
    {
        // Friday 16:00, add 2 business hours.
        // 1h remains Friday (to 17:00), 1h carries to Monday 09:00-10:00.
        var friday = new DateTime(2026, 10, 2, 16, 0, 0, DateTimeKind.Utc); // a Friday
        var monday = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

        var result = await Calculator.AddBusinessHours(friday, 2, _branchId, CancellationToken.None);

        result.Should().Be(monday);
    }

    [Fact]
    [Trait("AC", "226")]
    public async Task AC226_SkipsPublicHolidays()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<PublicHoliday>().Add(PublicHoliday.Create(
                _branchId, new DateOnly(2026, 10, 5), "Bridge holiday"));
            await db.SaveChangesAsync();
        }

        // Friday 16:00 + 9h: Fri 1h, Mon is a holiday (0h), Tue 8h -> Tuesday 17:00.
        var friday = new DateTime(2026, 10, 2, 16, 0, 0, DateTimeKind.Utc);
        var tuesday = new DateTime(2026, 10, 6, 17, 0, 0, DateTimeKind.Utc);

        var result = await Calculator.AddBusinessHours(friday, 9, _branchId, CancellationToken.None);

        result.Should().Be(tuesday);
    }

    [Fact]
    [Trait("AC", "227")]
    public async Task AC227_NoCalendarForBranch_FallsBackToWallClock()
    {
        var start = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);

        var result = await Calculator.AddBusinessHours(start, 4, Guid.NewGuid(), CancellationToken.None);

        result.Should().Be(start.AddHours(4));
    }
}
