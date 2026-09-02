using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-923 / AC-923.1 — the 3x3 matrix, exhaustively. The spec's table is the oracle.</summary>
public class PriorityMatrixTests
{
    [Theory]
    [Trait("AC", "923.1")]
    [InlineData("Low", "Low", "Low")]
    [InlineData("Low", "Medium", "Low")]
    [InlineData("Low", "High", "Normal")]
    [InlineData("Medium", "Low", "Low")]
    [InlineData("Medium", "Medium", "Normal")]
    [InlineData("Medium", "High", "High")]
    [InlineData("High", "Low", "Normal")]
    [InlineData("High", "Medium", "High")]
    [InlineData("High", "High", "Urgent")]
    public void Derives_The_Spec_Matrix(string impact, string urgency, string expectedPriority)
    {
        var derived = PriorityMatrix.Derive(TicketImpact.Create(impact), TicketUrgency.Create(urgency));

        derived.Value.Should().Be(expectedPriority);
    }

    [Theory]
    [Trait("AC", "923.1")]
    [InlineData("")]
    [InlineData("Critical")]
    public void Unknown_Impact_Is_Refused(string impact)
    {
        var act = () => TicketImpact.Create(impact);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [Trait("AC", "923.1")]
    [InlineData("")]
    [InlineData("Immediate")]
    public void Unknown_Urgency_Is_Refused(string urgency)
    {
        var act = () => TicketUrgency.Create(urgency);
        act.Should().Throw<ArgumentException>();
    }
}
