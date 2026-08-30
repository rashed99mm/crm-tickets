namespace CustomerSupport.Domain.Entities.Sla;

/// <summary>US-215, AC-226 — a whole-day exclusion for one branch. Two holiday rows for the same
/// date are harmless: both simply mark the day excluded.</summary>
public class PublicHoliday : BaseEntity
{
    public Guid BranchId { get; private set; }
    public DateOnly HolidayDate { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public static PublicHoliday Create(Guid branchId, DateOnly holidayDate, string name)
    {
        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("BranchId is required", nameof(branchId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        return new PublicHoliday
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            HolidayDate = holidayDate,
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
