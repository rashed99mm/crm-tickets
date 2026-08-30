using CustomerSupport.Domain.Entities.Identity;

namespace CustomerSupport.Application.Interfaces;

public interface IRefreshTokenService
{
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
    Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct = default);
    Task<bool> ValidateRefreshTokenAsync(Guid userId, string token, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null, string? revokedBy = null, CancellationToken ct = default);
    Task RevokeAllUserRefreshTokensAsync(Guid userId, string? revokedBy = null, CancellationToken ct = default);
    Task<int> CleanupExpiredTokensAsync(CancellationToken ct = default);
}
