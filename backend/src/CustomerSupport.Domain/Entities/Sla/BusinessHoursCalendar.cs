namespace CustomerSupport.Domain.Entities.Sla;

/// <summary>US-215, AC-225 — a working window for one weekday in one branch. Mirror of
/// <see cref="SLAPolicy"/>'s lookup-entity shape (no navigation, branch is a plain filter column).</summary>
public class BusinessHoursCalendar : BaseEntity
{
    public Guid BranchId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly OpenTime { get; private set; }
    public TimeOnly CloseTime { get; private set; }

    public static BusinessHoursCalendar Create(
        Guid branchId, DayOfWeek dayOfWeek, TimeOnly openTime, TimeOnly closeTime)
    {
        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("BranchId is required", nameof(branchId));
        }

        if (closeTime <= openTime)
        {
            throw new ArgumentException("CloseTime must be after OpenTime", nameof(closeTime));
        }

        return new BusinessHoursCalendar
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            DayOfWeek = dayOfWeek,
            OpenTime = openTime,
            CloseTime = closeTime,
            CreatedAt = DateTime.UtcNow
        };
    }
}
