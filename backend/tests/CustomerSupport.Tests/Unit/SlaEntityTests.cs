using CustomerSupport.Domain.Entities.Sla;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

public class SLAPolicyTests
{
    [Fact]
    public void Create_ValidFields_IsActive()
    {
        var policy = SLAPolicy.Create("High", 2, 24, null, null);

        policy.Priority.Should().Be("High");
        policy.ResponseTargetHours.Should().Be(2);
        policy.ResolutionTargetHours.Should().Be(24);
        policy.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveResponseTarget_Throws(decimal hours)
    {
        var act = () => SLAPolicy.Create("High", hours, 24, null, null);

        act.Should().Throw<ArgumentException>().WithParameterName("responseTargetHours");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveResolutionTarget_Throws(decimal hours)
    {
        var act = () => SLAPolicy.Create("High", 2, hours, null, null);

        act.Should().Throw<ArgumentException>().WithParameterName("resolutionTargetHours");
    }
}

public class SLAEventTests
{
    [Fact]
    public void Record_ValidFields_StoresThem()
    {
        var ticketId = Guid.NewGuid();
        var targetAt = DateTime.UtcNow;
        var breachedAt = targetAt.AddMinutes(5);

        var slaEvent = SLAEvent.Record(ticketId, SLAEvent.TargetTypes.Response, targetAt, breachedAt);

        slaEvent.TicketId.Should().Be(ticketId);
        slaEvent.TargetType.Should().Be("Response");
        slaEvent.TargetAt.Should().Be(targetAt);
        slaEvent.BreachedAt.Should().Be(breachedAt);
        slaEvent.PausedSeconds.Should().Be(0);
        slaEvent.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Record_UnrecognisedTargetType_Throws()
    {
        var act = () => SLAEvent.Record(Guid.NewGuid(), "Sideways", DateTime.UtcNow, null);

        act.Should().Throw<ArgumentException>().WithParameterName("targetType");
    }
}
