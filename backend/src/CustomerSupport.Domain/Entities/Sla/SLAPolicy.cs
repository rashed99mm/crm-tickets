namespace CustomerSupport.Domain.Entities.Sla;

/// <summary>
/// A target response/resolution time per priority (AC-124), optionally narrowed to a category
/// and/or a branch. The same lookup-entity shape as <see cref="Organisation.Department"/> — an
/// explicit <see cref="IsActive"/> flag, not the generic soft-delete one.
/// </summary>
public class SLAPolicy : BaseEntity
{
    public string Priority { get; private set; } = string.Empty;
    public decimal ResponseTargetHours { get; private set; }
    public decimal ResolutionTargetHours { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? BranchId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static SLAPolicy Create(
        string priority, decimal responseTargetHours, decimal resolutionTargetHours,
        Guid? categoryId, Guid? branchId)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException("Priority is required", nameof(priority));
        }

        if (responseTargetHours <= 0)
        {
            throw new ArgumentException("Response target hours must be positive", nameof(responseTargetHours));
        }

        if (resolutionTargetHours <= 0)
        {
            throw new ArgumentException("Resolution target hours must be positive", nameof(resolutionTargetHours));
        }

        return new SLAPolicy
        {
            Id = Guid.NewGuid(),
            Priority = priority.Trim(),
            ResponseTargetHours = responseTargetHours,
            ResolutionTargetHours = resolutionTargetHours,
            CategoryId = categoryId,
            BranchId = branchId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string priority, decimal responseTargetHours, decimal resolutionTargetHours,
        Guid? categoryId, Guid? branchId)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException("Priority is required", nameof(priority));
        }

        if (responseTargetHours <= 0)
        {
            throw new ArgumentException("Response target hours must be positive", nameof(responseTargetHours));
        }

        if (resolutionTargetHours <= 0)
        {
            throw new ArgumentException("Resolution target hours must be positive", nameof(resolutionTargetHours));
        }

        Priority = priority.Trim();
        ResponseTargetHours = responseTargetHours;
        ResolutionTargetHours = resolutionTargetHours;
        CategoryId = categoryId;
        BranchId = branchId;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }
}
