using CustomerSupport.Application.Interfaces;
using CustomerSupport.Infrastructure.Channels;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Channels;

/// <summary>
/// CC-47 / spec A24 — the per-IP fixed window behind the web form. A unit test with a controllable
/// clock, because the alternative is a test that sleeps for the window length.
/// </summary>
public class WebFormSubmissionThrottleTests
{
    private readonly Mock<IDateTimeService> _clock = new();
    private DateTime _now = new(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

    private WebFormSubmissionThrottle CreateSut()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(() => _now);
        return new WebFormSubmissionThrottle(_clock.Object);
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_WithinTheLimit_IsAllowed()
    {
        var sut = CreateSut();

        for (var i = 0; i < WebFormSubmissionThrottle.PermitLimit; i++)
        {
            sut.TryAcquire("10.0.0.1").Should().BeTrue($"submission {i + 1} is inside the limit");
        }
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_PastTheLimitInsideTheWindow_IsRefused()
    {
        var sut = CreateSut();
        for (var i = 0; i < WebFormSubmissionThrottle.PermitLimit; i++)
        {
            sut.TryAcquire("10.0.0.2");
        }

        sut.TryAcquire("10.0.0.2").Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_AfterTheWindowElapses_IsAllowedAgain()
    {
        var sut = CreateSut();
        for (var i = 0; i < WebFormSubmissionThrottle.PermitLimit; i++)
        {
            sut.TryAcquire("10.0.0.3");
        }

        _now = _now.Add(WebFormSubmissionThrottle.Window).AddSeconds(1);

        sut.TryAcquire("10.0.0.3").Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_OneClientsBurst_DoesNotThrottleAnother()
    {
        var sut = CreateSut();
        for (var i = 0; i < WebFormSubmissionThrottle.PermitLimit + 3; i++)
        {
            sut.TryAcquire("10.0.0.4");
        }

        sut.TryAcquire("10.0.0.5").Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC47")]
    public void CC47_ConcurrentAcquisitions_NeverExceedTheLimit()
    {
        var sut = CreateSut();

        var granted = 0;
        Parallel.For(0, 200, _ =>
        {
            if (sut.TryAcquire("10.0.0.6"))
            {
                Interlocked.Increment(ref granted);
            }
        });

        granted.Should().Be(WebFormSubmissionThrottle.PermitLimit);
    }
}
