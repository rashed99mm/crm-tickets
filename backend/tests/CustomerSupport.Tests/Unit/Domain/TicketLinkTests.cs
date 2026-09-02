using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-925 / AC-925.1 — what a link row itself can refuse (the cross-ticket guards are the handler's).</summary>
public class TicketLinkTests
{
    private static readonly Guid Source = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Target = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Theory]
    [Trait("AC", "925.1")]
    [InlineData("RelatedTo")]
    [InlineData("DuplicateOf")]
    public void Creates_A_Link_Of_Each_Type(string linkType)
    {
        var link = TicketLink.Create(Source, Target, linkType, Actor);

        link.SourceTicketId.Should().Be(Source);
        link.TargetTicketId.Should().Be(Target);
        link.LinkType.Should().Be(linkType);
        link.CreatedBy.Should().Be(Actor);
    }

    [Fact]
    [Trait("AC", "925.1")]
    public void Refuses_A_Self_Link()
    {
        var act = () => TicketLink.Create(Source, Source, "RelatedTo", Actor);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "925.1")]
    public void Refuses_An_Unknown_Link_Type()
    {
        var act = () => TicketLink.Create(Source, Target, "BlockedBy", Actor);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [Trait("AC", "925.1")]
    [InlineData("")]
    [InlineData("Related")]
    public void TicketLinkType_Refuses_Unknown_Values(string value)
    {
        var act = () => TicketLinkType.Create(value);
        act.Should().Throw<ArgumentException>();
    }
}
