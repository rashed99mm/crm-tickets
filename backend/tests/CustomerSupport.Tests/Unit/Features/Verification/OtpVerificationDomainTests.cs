using CustomerSupport.Domain.Entities.Verification;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Verification;

/// <summary>Domain invariants for <see cref="OtpVerification"/> (AC-439..AC-445).</summary>
public class OtpVerificationDomainTests
{
    [Fact]
    public void CanAttempt_TrueForFreshRecord()
    {
        Valid().CanAttempt(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void CanAttempt_FalseWhenExpired() // AC-440
    {
        var expired = Valid(expires: DateTime.UtcNow.AddMinutes(-1));
        expired.CanAttempt(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void RegisterFailedAttempt_LocksOnFifth() // AC-441
    {
        var v = Valid();
        for (var i = 0; i < OtpVerification.MaxFailedAttempts; i++)
        {
            v.RegisterFailedAttempt();
        }

        v.IsLocked.Should().BeTrue();
        v.CanAttempt(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void FifthFailureIsTheLockBoundary()
    {
        var v = Valid();
        for (var i = 0; i < OtpVerification.MaxFailedAttempts - 1; i++)
        {
            v.RegisterFailedAttempt();
        }

        v.IsLocked.Should().BeFalse();
        v.CanAttempt(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void MarkVerified_SetsVerifiedAndClosesAttempts() // AC-439
    {
        var v = Valid();
        v.MarkVerified(DateTime.UtcNow);

        v.IsVerified.Should().BeTrue();
        v.CanAttempt(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void CodeLength_IsSixDigits()
    {
        OtpVerification.CodeLength.Should().Be(6);
    }

    // ── OTP-3 cooldown ──────────────────────────────────────────────────────

    [Fact]
    public void Create_SetsLastSentToCreationInstant() // OTP-3
    {
        var created = DateTime.UtcNow;
        var v = OtpVerification.Create(Guid.NewGuid(), "+14155550100", OtpVerificationType.Phone, "hash", created.AddMinutes(5), created);

        v.LastSentAtUtc.Should().Be(created);
        v.RetryAfterSeconds(created).Should().Be(OtpVerification.ResendCooldownSeconds);
    }

    [Fact]
    public void CanRequest_FalseWithinCooldownWindow() // OTP-3
    {
        var created = DateTime.UtcNow;
        var v = Valid(createdAt: created);

        v.CanRequest(created.AddSeconds(OtpVerification.ResendCooldownSeconds - 1)).Should().BeFalse();
        v.RetryAfterSeconds(created.AddSeconds(OtpVerification.ResendCooldownSeconds - 1)).Should().Be(1);
    }

    [Fact]
    public void CanRequest_TrueAtCooldownBoundaryAndAfter() // OTP-3
    {
        var created = DateTime.UtcNow;
        var v = Valid(createdAt: created);

        v.CanRequest(created.AddSeconds(OtpVerification.ResendCooldownSeconds)).Should().BeTrue();
        v.CanRequest(created.AddSeconds(OtpVerification.ResendCooldownSeconds + 30)).Should().BeTrue();
        v.RetryAfterSeconds(created.AddSeconds(OtpVerification.ResendCooldownSeconds + 30)).Should().Be(0);
    }

    [Fact]
    public void CodeLifetime_IsFiveMinutes() // OTP-1, OTP-2
    {
        OtpVerification.CodeLifetime.Should().Be(TimeSpan.FromMinutes(5));
    }

    private static OtpVerification Valid(Guid? userId = null, DateTime? expires = null, DateTime? createdAt = null)
    {
        var created = createdAt ?? DateTime.UtcNow;
        return OtpVerification.Create(
            userId ?? Guid.NewGuid(),
            "+14155550100",
            OtpVerificationType.Phone,
            "hash",
            expires ?? created.AddMinutes(5),
            created);
    }
}
