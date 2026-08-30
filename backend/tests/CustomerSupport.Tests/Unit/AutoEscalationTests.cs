using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

/// <summary>
/// The two halves of US-218's auto-escalation that are pure domain and must stay exercisable
/// without a database: the <see cref="Ticket.AdvanceEscalation"/> transition (AC-218.1, AC-218.3)
/// and the terminal-level selection rule (AC-218.2). The persistence-backed provider and the
/// scanner's end-to-end behaviour live in the integration tests.
/// </summary>
public class EscalationLevelTests
{
    private static readonly Guid L1 = Guid.Parse("00000000-0000-0000-0000-0000000000E1");
    private static readonly Guid L2 = Guid.Parse("00000000-0000-0000-0000-0000000000E2");

    [Fact]
    public void Create_ValidFields_IsActive()
    {
        var level = EscalationLevel.Create("Level1", 60, "Agent", L1);

        level.Level.Should().Be("Level1");
        level.BreachMinutes.Should().Be(60);
        level.TargetRole.Should().Be("Agent");
        level.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingLevel_Throws(string level)
    {
        var act = () => EscalationLevel.Create(level, 60, null, L1);

        act.Should().Throw<ArgumentException>().WithParameterName("level");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveBreachMinutes_Throws(int minutes)
    {
        var act = () => EscalationLevel.Create("Level1", minutes, null, L1);

        act.Should().Throw<ArgumentException>().WithParameterName("breachMinutes");
    }

    /// <summary>AC-218.2: with nothing higher configured, the current level is terminal.</summary>
    [Fact]
    [Trait("AC", "218.2")]
    public void AC2182_EscalationPolicy_StopsAtHighestConfiguredLevel()
    {
        var ladder = new List<EscalationLevel>
        {
            EscalationLevel.Create("Level1", 60, "Agent", L1),
            EscalationLevel.Create("Level2", 240, "Supervisor", L2),
        };

        EscalationLevel.NextFrom(ladder, "Level0")!.Level.Should().Be("Level1");
        EscalationLevel.NextFrom(ladder, "Level1")!.Level.Should().Be("Level2");
        EscalationLevel.NextFrom(ladder, "Level2").Should().BeNull();
    }
}

/// <summary>The entity-side contract of the advancement — the caller (scanner) owns the decision of
/// *what* to advance to; the entity owns the correct recording of that decision.</summary>
public class TicketAdvanceEscalationTests
{
    private static readonly Guid Customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Category = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Supervisor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ArgumentNull = Guid.Empty;
    private static readonly Guid SystemEngine = Guid.Parse("E0000000-0000-0000-0000-000000000001");

    [Fact]
    [Trait("AC", "218.1")]
    public void AC2181_TicketAdvanceEscalation_RecordsPreviousAndNextLevel()
    {
        var ticket = Ticket.Create("TKT-001001", "Subject", "body", Customer, Category, "High", Supervisor);
        var historyBefore = ticket.History.Count;

        ticket.AdvanceEscalation(ticket.EscalationState, "Level1", SystemEngine);

        ticket.EscalationState.Should().Be("Level1");
        ticket.UpdatedBy.Should().Be(SystemEngine);
        ticket.History.Should().HaveCount(historyBefore + 1);
        var entry = ticket.History.Last();
        entry.ChangeType.Should().Be(TicketChangeType.Escalated.Value);
        entry.FromValue.Should().Be("None");
        entry.ToValue.Should().Be("Level1");
    }

    /// <summary>AC-218.3: a stale cursor — two concurrent passes both computing the same Level1 from
    /// "None", the loser calling after the winner already advanced the ticket — is refused without
    /// mutating state or history (the write must stay atomic). The stale case is *cursor already
    /// superseded*: state is Level1 but the caller still holds the old "None" cursor.</summary>
    [Fact]
    [Trait("AC", "218.3")]
    public void AC2183_EscalationTransition_RejectsDuplicateClaim()
    {
        var ticket = Ticket.Create("TKT-001001", "Subject", "body", Customer, Category, "High", Supervisor);
        ticket.AdvanceEscalation("None", "Level1", SystemEngine);
        var historyBefore = ticket.History.Count;

        var act = () => ticket.AdvanceEscalation("None", "Level1", SystemEngine);

        act.Should().Throw<InvalidOperationException>().WithMessage("*stale*")
            .Which.Should().NotBeNull();
        ticket.EscalationState.Should().Be("Level1");
        ticket.History.Should().HaveCount(historyBefore);
    }

    [Fact]
    [Trait("AC", "218.3")]
    public void AC2183_EscalationTransition_RejectsAdvancingToSameLevel()
    {
        var ticket = Ticket.Create("TKT-001001", "Subject", "body", Customer, Category, "High", Supervisor);
        ticket.AdvanceEscalation(ticket.EscalationState, "Level1", SystemEngine);
        var historyBefore = ticket.History.Count;

        var act = () => ticket.AdvanceEscalation("Level1", "Level1", SystemEngine);

        act.Should().Throw<InvalidOperationException>();
        ticket.History.Should().HaveCount(historyBefore);
    }

    [Fact]
    public void AdvanceEscalation_EmptySystemActor_Throws()
    {
        var ticket = Ticket.Create("TKT-001001", "Subject", "body", Customer, Category, "High", Supervisor);

        var act = () => ticket.AdvanceEscalation(ticket.EscalationState, "Level1", ArgumentNull);

        act.Should().Throw<ArgumentException>().WithParameterName("systemActor");
    }
}
