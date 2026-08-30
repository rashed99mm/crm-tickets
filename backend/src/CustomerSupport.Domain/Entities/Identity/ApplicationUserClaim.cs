using Microsoft.AspNetCore.Identity;

namespace CustomerSupport.Domain.Entities.Identity;

public class ApplicationUserClaim : IdentityUserClaim<Guid>
{
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public static ApplicationUserClaim Create(Guid userId, string claimType, string claimValue)
    {
        return new ApplicationUserClaim
        {
            UserId = userId,
            ClaimType = claimType,
            ClaimValue = claimValue,
            CreatedAt = DateTime.UtcNow
        };
    }
}
