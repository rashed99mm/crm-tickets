using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Sla;

/// <summary>US-215, AC-225..AC-227 — the business-hours-aware SLA target calculator. Reads the
/// branch's configured working windows and public holidays via <see cref="AppDbContext"/> directly
/// (the same pattern <c>SlaBreachScanner</c> uses for <c>db.Set&lt;SLAEvent&gt;()</c>), keeping the
/// persistence concern out of <c>Application</c> and <c>Domain</c>.</summary>
public sealed class BusinessHoursCalculator(AppDbContext db) : IBusinessHoursCalculator
{
    /// <summary>The bounded day-loop ceiling — ten years of calendar days is a generous upper bound
    /// for an SLA target that should always resolve in days, not years, and prevents an infinite
    /// spin when a branch has windows but the current cursor never lands inside one.</summary>
    private const int MaxDays = 3650;

    public async Task<DateTime> AddBusinessHours(DateTime start, decimal hours, Guid? branchId, CancellationToken ct)
    {
        if (branchId is not { } branch)
        {
            return start.AddHours((double)hours); // AC-227, no branch at all
        }

        var windows = await db.Set<BusinessHoursCalendar>().IgnoreQueryFilters()
            .Where(c => c.BranchId == branch).ToListAsync(ct);

        if (windows.Count == 0)
        {
            return start.AddHours((double)hours); // AC-227, no configured calendar
        }

        var holidays = (await db.Set<PublicHoliday>().IgnoreQueryFilters()
                .Where(h => h.BranchId == branch).ToListAsync(ct))
            .Select(h => h.HolidayDate).ToHashSet();

        var byDay = windows.ToDictionary(w => w.DayOfWeek);
        var remaining = hours;
        var cursor = start;

        for (var i = 0; i < MaxDays && remaining > 0; i++)
        {
            var date = DateOnly.FromDateTime(cursor.Date);
            if (holidays.Contains(date) || !byDay.TryGetValue(cursor.DayOfWeek, out var window))
            {
                cursor = cursor.Date.AddDays(1);
                continue;
            }

            var dayStart = cursor.TimeOfDay < window.OpenTime.ToTimeSpan()
                ? window.OpenTime.ToTimeSpan()
                : cursor.TimeOfDay;
            var dayEnd = window.CloseTime.ToTimeSpan();

            if (dayStart >= dayEnd)
            {
                cursor = cursor.Date.AddDays(1);
                continue;
            }

            var availableToday = (decimal)(dayEnd - dayStart).TotalHours;
            var used = Math.Min(remaining, availableToday);

            cursor = cursor.Date + dayStart + TimeSpan.FromHours((double)used);
            remaining -= used;

            if (remaining > 0)
            {
                cursor = cursor.Date.AddDays(1);
            }
        }

        return cursor;
    }
}
