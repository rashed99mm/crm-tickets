using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-924 / AC-924.1/2 — one normalization rule, stated once (spec A6).</summary>
public class TagValueTests
{
    [Theory]
    [Trait("AC", "924.1")]
    [InlineData("  Billing  ", "billing")]
    [InlineData("VIP   Customer", "vip customer")]
    [InlineData("password-reset", "password-reset")]
    [InlineData("BILLING", "billing")]
    public void Normalizes_Trim_Collapse_And_Case(string raw, string expected)
    {
        TagValue.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    [Trait("AC", "924.2")]
    public void Arabic_Tags_Survive_Normalization()
    {
        TagValue.Normalize(" فوترة ").Should().Be("فوترة");
    }

    [Theory]
    [Trait("AC", "924.1")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tag!")]
    [InlineData("tag_underscore")]
    [InlineData("semi;colon")]
    public void Refuses_Empty_And_Forbidden_Characters(string raw)
    {
        var act = () => TagValue.Normalize(raw);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "924.1")]
    public void Refuses_Values_Over_30_Chars_After_Normalization()
    {
        var act = () => TagValue.Normalize(new string('a', 31));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "924.1")]
    public void Accepts_Exactly_30_Chars()
    {
        TagValue.Normalize(new string('a', 30)).Should().HaveLength(30);
    }

    [Fact]
    [Trait("AC", "924.1")]
    public void TicketTag_Create_Stores_The_Normalized_Value()
    {
        var ticketId = Guid.NewGuid();
        var actor = Guid.NewGuid();

        var tag = TicketTag.Create(ticketId, "  Billing ISSUE ", actor);

        tag.TicketId.Should().Be(ticketId);
        tag.Value.Should().Be("billing issue");
        tag.CreatedBy.Should().Be(actor);
    }
}
