using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>
/// The 8-state transition table (AC-501, AC-502). Isolated from the entity that consults it.
///
/// Two theories, and the second is the one that matters: it derives the refused set as the
/// *complement* of the permitted set over all 64 pairs, so a transition someone quietly adds to
/// <see cref="TicketStatus"/> without amending the spec fails here rather than passing unnoticed.
/// Self-transitions and illegal transitions fall out of that complement automatically.
/// </summary>
public class TicketStatusTests
{
    /// <summary>The 12 legal pairs of the 8-state machine (AC-501).</summary>
    public static TheoryData<string, string> PermittedTransitions => new()
    {
        { "New", "Open" },
        { "Open", "Assigned" },
        { "Open", "Resolved" },
        { "Assigned", "In Progress" },
        { "In Progress", "Waiting for Customer" },
        { "In Progress", "Waiting for Internal Team" },
        { "In Progress", "Resolved" },
        { "Waiting for Customer", "In Progress" },
        { "Waiting for Internal Team", "In Progress" },
        { "Resolved", "In Progress" },
        { "Resolved", "Closed" },
        { "Closed", "In Progress" },
    };

    [Theory]
    [MemberData(nameof(PermittedTransitions))]
    [Trait("AC", "501")]
    public void TicketStatus_AllowsEachLegalTransition(string from, string to)   // US-901 TC-01
    {
        TicketStatus.Create(from).CanTransitionTo(TicketStatus.Create(to)).Should().BeTrue();
    }

    /// <summary>All 64 pairs minus the 12 permitted — 52 refusals including every self-transition.</summary>
    public static TheoryData<string, string> RefusedTransitions
    {
        get
        {
            var data = new TheoryData<string, string>();
            var all = TicketStatus.All.Select(s => s.Value).ToArray();
            var permitted = new HashSet<(string, string)>(
                PermittedTransitions.Select(t => ((string)t[0], (string)t[1])));
            foreach (var from in all)
            foreach (var to in all)
            {
                if (!permitted.Contains((from, to)))
                {
                    data.Add(from, to);
                }
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(RefusedTransitions))]
    [Trait("AC", "502")]
    public void TicketStatus_RefusesEveryIllegalTransition(string from, string to)   // US-901 TC-02
    {
        TicketStatus.Create(from).CanTransitionTo(TicketStatus.Create(to)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "502")]
    public void Create_RejectsStatusesOutsideTheEight()   // guards 400-vs-409 (AC-30 survives)
    {
        var act = () => TicketStatus.Create("Pending");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "507")]
    public void TicketStatus_All_DoesNotIncludeEscalated()   // US-904 TC-02
    {
        TicketStatus.All.Should().HaveCount(8);
        TicketStatus.All.Select(s => s.Value).Should().NotContain("Escalated");
    }
}
