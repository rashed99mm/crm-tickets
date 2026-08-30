using Microsoft.AspNetCore.Identity;

namespace CustomerSupport.Domain.Entities.Identity;

public class ApplicationUserToken : IdentityUserToken<Guid>
{
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public static ApplicationUserToken Create(Guid userId, string loginProvider, string name, string value)
    {
        return new ApplicationUserToken
        {
            UserId = userId,
            LoginProvider = loginProvider,
            Name = name,
            Value = value,
            CreatedAt = DateTime.UtcNow
        };
    }
}
