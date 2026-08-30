using Microsoft.AspNetCore.Identity;

namespace CustomerSupport.Domain.Entities.Identity;

public class ApplicationUserRole : IdentityUserRole<Guid>
{
    public DateTime AssignedAt { get; private set; } = DateTime.UtcNow;
    public string? AssignedBy { get; private set; }

    public static ApplicationUserRole Create(Guid userId, Guid roleId, string? assignedBy = null)
    {
        return new ApplicationUserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy
        };
    }
}
