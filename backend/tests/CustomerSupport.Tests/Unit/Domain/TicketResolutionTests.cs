using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-922 — how a ticket was resolved is recorded, and reopening is counted (AC-922.2/4/5).</summary>
public class TicketResolutionTests
{
    private static readonly Guid Customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Category = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Supervisor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Agent = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly ResolutionDetails Resolution = new("Fixed", "Reset the password and confirmed sign-in.");

    private static Ticket TicketInProgress()
    {
        var ticket = Ticket.Create("TKT-001000", "Cannot sign in", "The portal rejects my password.",
            Customer, Category, "Medium", "Medium", Supervisor);
        ticket.AssignTo(Agent, Supervisor);
        ticket.ChangeStatus("Open", Agent);
        ticket.ChangeStatus("Assigned", Agent);
        ticket.ChangeStatus("In Progress", Agent);
        return ticket;
    }

    [Fact]
    [Trait("AC", "922.5")]
    public void Resolving_Without_Details_Is_Refused()
    {
        var ticket = TicketInProgress();

        var act = () => ticket.ChangeStatus("Resolved", Agent);

        act.Should().Throw<InvalidOperationException>().WithMessage("*resolution*");
        ticket.Status.Should().Be("In Progress");
        ticket.ResolutionCode.Should().BeNull();
    }

    [Fact]
    [Trait("AC", "922.2")]
    public void Resolving_With_Details_Stamps_Code_Notes_And_ResolvedAt()
    {
        var ticket = TicketInProgress();

        ticket.ChangeStatus("Resolved", Agent, Resolution);

        ticket.Status.Should().Be("Resolved");
        ticket.ResolutionCode.Should().Be("Fixed");
        ticket.ResolutionNotes.Should().Be("Reset the password and confirmed sign-in.");
        ticket.ResolvedAt.Should().NotBeNull();
        ticket.History.Should().Contain(h => h.ChangeType == "StatusChanged" && h.ToValue == "Resolved");
    }

    [Fact]
    [Trait("AC", "922.3")]
    public void An_Unknown_Resolution_Code_Is_Refused()
    {
        var ticket = TicketInProgress();

        var act = () => ticket.ChangeStatus("Resolved", Agent, new ResolutionDetails("Solved", "notes"));

        act.Should().Throw<ArgumentException>();
        ticket.Status.Should().Be("In Progress");
    }

    [Theory]
    [Trait("AC", "922.3")]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_Resolution_Notes_Are_Refused(string notes)
    {
        var ticket = TicketInProgress();

        var act = () => ticket.ChangeStatus("Resolved", Agent, new ResolutionDetails("Fixed", notes));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "922.3")]
    public void Resolution_Notes_Over_2000_Chars_Are_Refused()
    {
        var ticket = TicketInProgress();

        var act = () => ticket.ChangeStatus("Resolved", Agent, new ResolutionDetails("Fixed", new string('x', 2001)));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "922.4")]
    public void Reopening_Clears_Resolution_And_Increments_ReopenCount()
    {
        var ticket = TicketInProgress();
        ticket.ChangeStatus("Resolved", Agent, Resolution);

        ticket.ChangeStatus("In Progress", Agent); // reopen (IsReopenTo targets In Progress)

        ticket.ReopenCount.Should().Be(1);
        ticket.ResolutionCode.Should().BeNull();
        ticket.ResolutionNotes.Should().BeNull();
        ticket.ResolvedAt.Should().BeNull();
        ticket.History.Should().Contain(h => h.ChangeType == "Reopened");
    }

    [Fact]
    [Trait("AC", "922.4")]
    public void Every_Reopen_Counts()
    {
        var ticket = TicketInProgress();
        ticket.ChangeStatus("Resolved", Agent, Resolution);
        ticket.ChangeStatus("In Progress", Agent);
        ticket.ChangeStatus("Resolved", Agent, Resolution);
        ticket.ChangeStatus("In Progress", Agent);

        ticket.ReopenCount.Should().Be(2);
    }

    [Fact]
    [Trait("AC", "922.2")]
    public void Closing_A_Resolved_Ticket_Keeps_The_Resolution()
    {
        var ticket = TicketInProgress();
        ticket.ChangeStatus("Resolved", Agent, Resolution);

        ticket.ChangeStatus("Closed", Agent);

        ticket.ResolutionCode.Should().Be("Fixed");
        ticket.ResolutionNotes.Should().NotBeNull();
    }
}
