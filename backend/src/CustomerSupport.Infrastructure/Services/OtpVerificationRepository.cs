using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Verification;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// EF-backed <see cref="IOtpVerificationRepository"/>. Shares the host's scoped
/// <see cref="AppDbContext"/> with <see cref="IdentityUserService"/>, so a verify handler can persist
/// the OTP record and the linked Identity confirmation in a single <c>SaveChangesAsync</c> (AC-444).
/// </summary>
public class OtpVerificationRepository : IOtpVerificationRepository
{
    private readonly AppDbContext _dbContext;

    public OtpVerificationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OtpVerification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbContext.OtpVerifications.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<OtpVerification?> GetLatestForUserAsync(
        Guid userId, string contact, OtpVerificationType type, CancellationToken ct = default) =>
        await _dbContext.OtpVerifications
            .Where(x => x.UserId == userId && x.Contact == contact && x.Type == type)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(OtpVerification entity, CancellationToken ct = default)
    {
        _dbContext.OtpVerifications.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translate to a domain exception so the Application layer handles the race without an
            // Entity Framework dependency (AC-442).
            throw new ConcurrencyException("The verification record was modified concurrently.", ex);
        }
    }
}
