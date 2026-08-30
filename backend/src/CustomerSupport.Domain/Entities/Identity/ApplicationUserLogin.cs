using Microsoft.AspNetCore.Identity;

namespace CustomerSupport.Domain.Entities.Identity;

public class ApplicationUserLogin : IdentityUserLogin<Guid>
{
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public static ApplicationUserLogin Create(Guid userId, string loginProvider, string providerKey, string? displayName)
    {
        return new ApplicationUserLogin
        {
            UserId = userId,
            LoginProvider = loginProvider,
            ProviderKey = providerKey,
            ProviderDisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        };
    }
}
