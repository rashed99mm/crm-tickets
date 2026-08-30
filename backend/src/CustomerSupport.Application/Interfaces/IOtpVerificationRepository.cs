using CustomerSupport.Domain.Entities.Verification;

namespace CustomerSupport.Application.Interfaces;

/// <summary>
/// Persistence port for <see cref="OtpVerification"/>. The implementation is EF-backed and shares the
/// host's scoped <c>AppDbContext</c>, so a verification write and the linked Identity confirmation
/// write from the same handler commit in one unit of work (AC-444).
/// </summary>
public interface IOtpVerificationRepository
{
    Task<OtpVerification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the newest record for a user's contact and channel — the OTP-3 cooldown anchor.</summary>
    Task<OtpVerification?> GetLatestForUserAsync(Guid userId, string contact, OtpVerificationType type, CancellationToken ct = default);

    Task AddAsync(OtpVerification entity, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
