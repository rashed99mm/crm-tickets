using System;

namespace CustomerSupport.Domain.Entities.Verification;

/// <summary>
/// A single-purpose, short-lived proof-of-possession record for an email or phone contact.
///
/// Only a one-way <see cref="CodeHash"/> is stored — never the plaintext code, which is generated
/// elsewhere, dispatched through a channel, and discarded. Verification compares a hash of the
/// submitted code and, on success, flips the linked Identity confirmation flag (see the verify
/// handler). A rowversion <see cref="RowVersion"/> gives the optimistic-concurrency guarantee that
/// two concurrent successful submissions cannot both win (AC-442).
/// </summary>
public class OtpVerification
{
    /// <summary>AC-441 — at the fifth failed attempt the record locks and no further compare runs.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>AC-440 — exactly six ASCII digits is the only accepted shape.</summary>
    public const int CodeLength = 6;

    /// <summary>OTP-1/OTP-2 — a freshly issued code is valid for five minutes.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    /// <summary>OTP-3 — a new request for the same contact and channel is refused within this window.</summary>
    public const int ResendCooldownSeconds = 60;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Contact { get; private set; } = string.Empty;
    public OtpVerificationType Type { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>UTC timestamp of the last dispatch for this contact and channel — the OTP-3 cooldown anchor.</summary>
    public DateTime? LastSentAtUtc { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public bool IsInvalidated { get; private set; }

    /// <summary>SQL <c>rowversion</c>; EF enforces the optimistic-concurrency check on write.</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private OtpVerification() { }

    public static OtpVerification Create(
        Guid userId,
        string contact,
        OtpVerificationType type,
        string codeHash,
        DateTime expiresAtUtc,
        DateTime createdAtUtc)
    {
        return new OtpVerification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Contact = contact,
            Type = type,
            CodeHash = codeHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = createdAtUtc,
            LastSentAtUtc = createdAtUtc,
        };
    }

    /// <summary>OTP-3 — another code may be requested once the cooldown has elapsed.</summary>
    public bool CanRequest(DateTime nowUtc) =>
        LastSentAtUtc is null || nowUtc >= LastSentAtUtc.Value.AddSeconds(ResendCooldownSeconds);

    /// <summary>Whole seconds remaining until the OTP-3 cooldown clears, for response metadata only.</summary>
    public int RetryAfterSeconds(DateTime nowUtc)
    {
        if (LastSentAtUtc is not { } lastSent)
        {
            return 0;
        }

        var remaining = ResendCooldownSeconds - (int)(nowUtc - lastSent).TotalSeconds;
        return remaining > 0 ? remaining : 0;
    }

    public bool IsExpired(DateTime nowUtc) => nowUtc > ExpiresAtUtc;

    public bool IsLocked => FailedAttemptCount >= MaxFailedAttempts;

    /// <summary>True only when the record can still be attempted: not verified, not invalidated,
    /// not expired, and not locked.</summary>
    public bool CanAttempt(DateTime nowUtc) =>
        !IsVerified && !IsInvalidated && !IsExpired(nowUtc) && !IsLocked;

    /// <summary>Records a failed attempt. The fifth failure locks the record (AC-441).</summary>
    public void RegisterFailedAttempt() => FailedAttemptCount++;

    public void MarkVerified(DateTime nowUtc)
    {
        IsVerified = true;
        VerifiedAtUtc = nowUtc;
    }

    public void Invalidate() => IsInvalidated = true;
}
