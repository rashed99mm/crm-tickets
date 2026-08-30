using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Security;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _dbContext;

    public RefreshTokenService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var expirationDays = 14;
        var refreshToken = RefreshToken.Create(
            userId,
            GenerateSecureToken(),
            TimeSpan.FromDays(expirationDays),
            ipAddress,
            userAgent
        );

        await _dbContext.RefreshTokens.AddAsync(refreshToken, ct);
        await _dbContext.SaveChangesAsync(ct);

        return refreshToken;
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        return await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(rt => rt.Token == token, ct);
    }

    public async Task<bool> ValidateRefreshTokenAsync(Guid userId, string token, CancellationToken ct = default)
    {
        var refreshToken = await GetRefreshTokenAsync(token, ct);
        return refreshToken != null && refreshToken.UserId == userId && refreshToken.IsActive;
    }

    public async Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null, string? revokedBy = null, CancellationToken ct = default)
    {
        var refreshToken = await GetRefreshTokenAsync(token, ct);
        if (refreshToken != null)
        {
            refreshToken.Revoke(replacedByToken, revokedBy);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeAllUserRefreshTokensAsync(Guid userId, string? revokedBy = null, CancellationToken ct = default)
    {
        // IsRevoked is a computed property (RevokedAt.HasValue), not a mapped column, so EF
        // cannot translate it into SQL. Filter on the mapped column instead.
        var tokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.Revoke(null, revokedBy);
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> CleanupExpiredTokensAsync(CancellationToken ct = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        return await _dbContext.RefreshTokens
            .Where(rt => rt.ExpiresAt < cutoffDate)
            .ExecuteDeleteAsync(ct);
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
