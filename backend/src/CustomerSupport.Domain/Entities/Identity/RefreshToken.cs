namespace CustomerSupport.Domain.Entities.Identity;

public class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedBy { get; private set; }
    public string? ReplacedByToken { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public virtual ApplicationUser User { get; private set; } = null!;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsExpired && !IsRevoked;

    public static RefreshToken Create(Guid userId, string token, TimeSpan expiresIn, string? ipAddress = null, string? userAgent = null)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expiresIn),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    }

    public void Revoke(string? replacedByToken = null, string? revokedBy = null)
    {
        if (IsRevoked)
            return;

        RevokedAt = DateTime.UtcNow;
        ReplacedByToken = replacedByToken;
        RevokedBy = revokedBy;
    }

    public bool IsTokenValid(string token)
    {
        return Token == token && IsActive;
    }
}
