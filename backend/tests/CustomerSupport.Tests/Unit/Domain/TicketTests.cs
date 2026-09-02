using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>
/// The ticket aggregate. The transition table itself is covered exhaustively in
/// <see cref="TicketStatusTests"/>; what is tested here is only what the *entity* adds on top of
/// it — creation defaults (AC-29), the history appended by each change (AC-48, AC-503), and the
/// ownership question no endpoint policy can answer (AC-45, AC-46).
///
/// No database, deliberately: this is the most-tested logic in the slice and it has to stay
/// exercisable without infrastructure for that to remain true.
/// </summary>
public class TicketTests
{
    private static readonly Guid Customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Category = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Supervisor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Agent = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherAgent = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Specialist = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid OtherSpecialist = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static Ticket NewTicket(string impact = "Medium", string urgency = "Medium") =>
        Ticket.Create("TKT-001000", "Cannot sign in", "The portal rejects my password.",
            Customer, Category, impact, urgency, Supervisor);

    /// <summary>
    /// Builds a ticket in the named status, optionally ensuring it has an assignee (required for
    /// entering work states per AC-505).
    /// </summary>
    private static Ticket TicketAt(string status, bool assigned = false)
    {
        var ticket = NewTicket();

        // Assign early when the target status is a work state, because AC-505 requires it.
        if (assigned || status is "In Progress" or "Waiting for Customer" or "Waiting for Internal Team")
        {
            ticket.AssignTo(Agent, Supervisor);
        }

        string[] path = status switch
        {
            "New" => [],
            "Open" => ["Open"],
            "Assigned" => ["Open", "Assigned"],
            "In Progress" => ["Open", "Assigned", "In Progress"],
            "Waiting for Customer" => ["Open", "Assigned", "In Progress", "Waiting for Customer"],
            "Waiting for Internal Team" => ["Open", "Assigned", "In Progress", "Waiting for Internal Team"],
            "Resolved" => ["Open", "Resolved"],
            "Closed" => ["Open", "Resolved", "Closed"],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        foreach (var step in path)
        {
            ticket.ChangeStatus(step, Agent, step == "Resolved" ? DefaultResolution : null);
        }

        return ticket;
    }

    private static readonly ResolutionDetails DefaultResolution = new("Fixed", "resolved in test");

    [Fact]
    [Trait("AC", "29")]
    public void AC29_A_New_Ticket_Starts_New_And_Unassigned()
    {
        var ticket = NewTicket();

        ticket.Status.Should().Be("New");
        ticket.AssigneeId.Should().BeNull();
        ticket.Reference.Should().Be("TKT-001000");
    }

    [Theory]
    [Trait("AC", "30")]
    [InlineData("", "subject")]
    [InlineData("   ", "subject")]
    public void AC30_Create_Rejects_A_Missing_Subject(string subject, string expectedParameter)
    {
        var act = () => Ticket.Create("TKT-001000", subject, "body", Customer, Category, "Medium", "Medium", Supervisor);

        act.Should().Throw<ArgumentException>().WithParameterName(expectedParameter);
    }

    [Fact]
    [Trait("AC", "30")]
    public void AC30_Create_Rejects_A_Subject_Over_Its_Length_Limit()
    {
        var act = () => Ticket.Create("TKT-001000", new string('x', 201), "body", Customer, Category, "Medium", "Medium", Supervisor);

        act.Should().Throw<ArgumentException>().WithParameterName("subject");
    }

    [Fact]
    [Trait("AC", "923.1")]
    public void AC30_Create_Rejects_An_Impact_Outside_The_Three()
    {
        var act = () => Ticket.Create("TKT-001000", "Subject", "body", Customer, Category, "Catastrophic", "Medium", Supervisor);

        act.Should().Throw<ArgumentException>().WithMessage("*Catastrophic*");
    }

    [Fact]
    [Trait("AC", "48")]
    public void AC48_Creation_Appends_A_Created_Row_Naming_The_Actor()
    {
        var entry = NewTicket().History.Should().ContainSingle().Subject;

        entry.ChangeType.Should().Be("Created");
        entry.ActorId.Should().Be(Supervisor);
        entry.FromValue.Should().BeNull();
        entry.ToValue.Should().Be("New");
    }

    [Fact]
    [Trait("AC", "48")]
    public void AC48_A_Status_Change_Appends_A_Row_Carrying_Both_Values()
    {
        var ticket = NewTicket();

        ticket.ChangeStatus("Open", Supervisor);

        var entry = ticket.History.Last();
        entry.ChangeType.Should().Be("StatusChanged");
        entry.FromValue.Should().Be("New");
        entry.ToValue.Should().Be("Open");
    }

    [Theory]
    [Trait("AC", "38")]
    [InlineData("New", "Closed")]
    [InlineData("Closed", "Resolved")]
    [InlineData("Open", "Open")]
    public void AC38_A_Refused_Transition_Changes_Neither_Status_Nor_History(string from, string to)
    {
        var ticket = TicketAt(from);
        var historyBefore = ticket.History.Count;

        var act = () => ticket.ChangeStatus(to, Supervisor);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{from}*{to}*");
        ticket.Status.Should().Be(from);
        ticket.History.Should().HaveCount(historyBefore);
    }

    [Theory]
    [Trait("AC", "503")]
    [InlineData("Resolved")]
    [InlineData("Closed")]
    public void Ticket_Reopening_RecordsReopenedHistory(string from)   // US-901 TC-03
    {
        var ticket = TicketAt(from, assigned: true);

        ticket.ChangeStatus("In Progress", Agent);

        ticket.Status.Should().Be("In Progress");
        var entry = ticket.History.Last();
        entry.ChangeType.Should().Be("Reopened");
        entry.FromValue.Should().Be(from);
        entry.ToValue.Should().Be("In Progress");
    }

    [Fact]
    [Trait("AC", "510")]
    public void Ticket_Resolve_Close_StampAndReopenClears()   // US-906 TC-02
    {
        var ticket = TicketAt("In Progress", assigned: true);

        ticket.ChangeStatus("Resolved", Agent, DefaultResolution);
        ticket.ResolvedAt.Should().NotBeNull();
        ticket.ClosedAt.Should().BeNull();

        ticket.ChangeStatus("Closed", Agent);
        ticket.ClosedAt.Should().NotBeNull();

        ticket.ChangeStatus("In Progress", Agent);
        ticket.ResolvedAt.Should().BeNull();
        ticket.ClosedAt.Should().BeNull();
        ticket.Status.Should().Be("In Progress");
    }

    [Fact]
    [Trait("AC", "510")]
    public void Ticket_RecordResponse_SetsFirstAndLast()   // US-906 TC-01
    {
        var ticket = NewTicket();
        var first = DateTime.UtcNow.AddMinutes(-5);

        ticket.RecordResponse(first);

        ticket.FirstResponseAt.Should().Be(first);
        ticket.LastResponseAt.Should().Be(first);

        var second = DateTime.UtcNow;
        ticket.RecordResponse(second);

        ticket.FirstResponseAt.Should().Be(first);
        ticket.LastResponseAt.Should().Be(second);
    }

    [Fact]
    [Trait("AC", "511")]
    public void Ticket_Assign_PropagatesOrg()   // US-907 TC-01
    {
        var ticket = NewTicket();
        var org = (Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd1"),
                   Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd2"),
                   Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd3"));

        ticket.InheritOrganisation(org.Item1, org.Item2, org.Item3);

        ticket.DepartmentId.Should().Be(org.Item1);
        ticket.BranchId.Should().Be(org.Item2);
        ticket.TeamId.Should().Be(org.Item3);
    }

    [Fact]
    [Trait("AC", "506")]
    public void Ticket_TakeEscalation_SetsOwner_RecordsHistory()   // US-904 TC-01
    {
        var ticket = TicketAt("In Progress", assigned: true);
        ticket.Escalate("Level1");

        ticket.TakeEscalation(Specialist, Supervisor);

        ticket.EscalationAssigneeId.Should().Be(Specialist);
        ticket.EscalationState.Should().Be("Level1");
        var entry = ticket.History.Last();
        entry.ChangeType.Should().Be("Escalated");
        entry.FromValue.Should().BeNull();
        entry.ToValue.Should().Be(Specialist.ToString());

        ticket.TakeEscalation(OtherSpecialist, Supervisor);
        ticket.History.Last().ChangeType.Should().Be("Escalated");
        ticket.History.Last().FromValue.Should().Be(Specialist.ToString());
        ticket.History.Last().ToValue.Should().Be(OtherSpecialist.ToString());
    }

    [Fact]
    [Trait("AC", "505")]
    public void Ticket_EnteringWorkState_WithoutAssignee_Throws()   // US-903 AC1
    {
        var ticket = TicketAt("Assigned", assigned: false);

        var act = () => ticket.ChangeStatus("In Progress", Agent);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be assigned*");
        ticket.Status.Should().Be("Assigned");
    }

    [Fact]
    [Trait("AC", "505")]
    public void Ticket_EnteringWorkState_WhenAssigned_Proceeds()
    {
        var ticket = TicketAt("Assigned", assigned: true);

        ticket.ChangeStatus("In Progress", Agent);

        ticket.Status.Should().Be("In Progress");
    }

    [Theory]
    [Trait("AC", "505")]
    [InlineData("In Progress")]
    [InlineData("Waiting for Customer")]
    [InlineData("Waiting for Internal Team")]
    public void Ticket_WorkStates_RequireAssignee(string workStatus)
    {
        var ticket = TicketAt("Assigned", assigned: false);

        var act = () => ticket.ChangeStatus(workStatus, Agent);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("AC", "504")]
    public void Ticket_SlaPause_WaitingForCustomer_ShiftsDueDates()
    {
        var ticket = TicketAt("In Progress", assigned: true);
        var originalDue = DateTime.UtcNow.AddHours(4);
        ticket.SetSlaTargets(originalDue, originalDue);

        ticket.ChangeStatus("Waiting for Customer", Agent);
        var pausedAt = ticket.PausedAt;
        pausedAt.Should().NotBeNull();

        ticket.ChangeStatus("In Progress", Agent);
        ticket.PausedAt.Should().BeNull();
        ticket.TotalPausedSeconds.Should().BeGreaterThan(0);
        ticket.ResponseDueAt.Should().BeAfter(originalDue);
        ticket.ResolutionDueAt.Should().BeAfter(originalDue);
    }

    [Fact]
    [Trait("AC", "504")]
    public void Ticket_SlaPause_WaitingForInternalTeam_ShiftsDueDates()
    {
        var ticket = TicketAt("In Progress", assigned: true);
        var originalDue = DateTime.UtcNow.AddHours(4);
        ticket.SetSlaTargets(originalDue, originalDue);

        ticket.ChangeStatus("Waiting for Internal Team", Agent);
        var pausedAt = ticket.PausedAt;
        pausedAt.Should().NotBeNull();

        ticket.ChangeStatus("In Progress", Agent);
        ticket.PausedAt.Should().BeNull();
        ticket.TotalPausedSeconds.Should().BeGreaterThan(0);
        ticket.ResponseDueAt.Should().BeAfter(originalDue);
        ticket.ResolutionDueAt.Should().BeAfter(originalDue);
    }

    [Fact]
    [Trait("AC", "48")]
    public void AC48_A_First_Assignment_Is_Assigned_And_A_Later_One_Is_Reassigned()
    {
        var ticket = NewTicket();

        ticket.AssignTo(Agent, Supervisor);
        var first = ticket.History.Last();

        ticket.AssignTo(OtherAgent, Supervisor);
        var second = ticket.History.Last();

        first.ChangeType.Should().Be("Assigned");
        first.FromValue.Should().BeNull();
        first.ToValue.Should().Be(Agent.ToString());

        second.ChangeType.Should().Be("Reassigned");
        second.FromValue.Should().Be(Agent.ToString());
        second.ToValue.Should().Be(OtherAgent.ToString());
    }

    [Fact]
    [Trait("AC", "45")]
    [Trait("AC", "46")]
    public void AC45_AC46_Ownership_Is_The_Assignee_Only_And_Unassigned_Belongs_To_Nobody()
    {
        var unassigned = NewTicket();
        var assigned = NewTicket();
        assigned.AssignTo(Agent, Supervisor);

        assigned.IsAssignedTo(Agent).Should().BeTrue();
        assigned.IsAssignedTo(OtherAgent).Should().BeFalse();
        unassigned.IsAssignedTo(Agent).Should().BeFalse();
    }

    // --- FEAT-24..27 — channel origin (CC-2/CC-11) ------------------------------------------------

    [Fact]
    public void A_New_Ticket_Has_No_Source_Channel()
    {
        NewTicket().Source.Should().BeNull();
    }

    [Fact]
    public void SetSource_StoresTheChannel_OnlyOnceAtCreationTime()
    {
        var ticket = NewTicket();

        ticket.SetSource("WhatsApp");

        ticket.Source.Should().Be("WhatsApp");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SetSource_EmptyOrNull_Throws(string? source)
    {
        var act = () => NewTicket().SetSource(source);

        act.Should().Throw<ArgumentException>().WithParameterName("source");
    }
}
