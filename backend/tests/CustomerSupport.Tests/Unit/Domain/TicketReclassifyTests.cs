using CustomerSupport.Domain.Entities.Tickets;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-923 — creation derives, reclassify re-derives and records (AC-923.1/2).</summary>
public class TicketReclassifyTests
{
    private static readonly Guid Customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Category = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Supervisor = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static Ticket NewTicket(string impact = "Medium", string urgency = "Medium") =>
        Ticket.Create("TKT-001000", "Cannot sign in", "The portal rejects my password.",
            Customer, Category, impact, urgency, Supervisor);

    [Fact]
    [Trait("AC", "923.1")]
    public void Creation_Derives_Priority_From_The_Matrix()
    {
        var ticket = NewTicket("High", "High");

        ticket.Impact.Should().Be("High");
        ticket.Urgency.Should().Be("High");
        ticket.Priority.Should().Be("Urgent");
    }

    [Fact]
    [Trait("AC", "923.2")]
    public void Reclassify_Rederives_And_Records_History_When_Priority_Changes()
    {
        var ticket = NewTicket("Medium", "Medium"); // Normal

        ticket.Reclassify("High", "High", Supervisor); // Urgent

        ticket.Priority.Should().Be("Urgent");
        ticket.History.Should().Contain(h =>
            h.ChangeType == "Reprioritized" && h.FromValue == "Normal" && h.ToValue == "Urgent");
    }

    [Fact]
    [Trait("AC", "923.2")]
    public void Reclassify_Without_A_Priority_Change_Writes_No_History_Row()
    {
        var ticket = NewTicket("Medium", "Medium"); // Normal

        ticket.Reclassify("Low", "High", Supervisor); // still Normal

        ticket.Impact.Should().Be("Low");
        ticket.Urgency.Should().Be("High");
        ticket.Priority.Should().Be("Normal");
        ticket.History.Should().NotContain(h => h.ChangeType == "Reprioritized");
    }

    [Fact]
    [Trait("AC", "923.2")]
    public void Reclassify_Requires_An_Actor()
    {
        var ticket = NewTicket();

        var act = () => ticket.Reclassify("High", "High", Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
