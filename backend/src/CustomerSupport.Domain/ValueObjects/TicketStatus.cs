namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// The eight ticket lifecycle states and the closed table of transitions between them
/// (AC-501, AC-502, AC-503; supersedes the five-state AC-37..AC-40 table). Persisted as a string,
/// never as an int: reordering this type must not renumber existing rows. Escalation is a marker
/// (<see cref="EscalationState"/>/<see cref="EscalationAssigneeId"/>), never a status (AC-507) —
/// it is deliberately absent from <see cref="All"/>.
/// </summary>
public sealed class TicketStatus : ValueObject
{
    public string Value { get; }

    public static readonly TicketStatus New = new("New");
    public static readonly TicketStatus Open = new("Open");
    public static readonly TicketStatus Assigned = new("Assigned");
    public static readonly TicketStatus InProgress = new("In Progress");
    public static readonly TicketStatus WaitingForCustomer = new("Waiting for Customer");
    public static readonly TicketStatus WaitingForInternalTeam = new("Waiting for Internal Team");
    public static readonly TicketStatus Resolved = new("Resolved");
    public static readonly TicketStatus Closed = new("Closed");

    /// <summary>Every status, in lifecycle order. Escalated is not here — escalation is a marker (AC-507).</summary>
    public static IReadOnlyList<TicketStatus> All { get; } =
        [New, Open, Assigned, InProgress, WaitingForCustomer, WaitingForInternalTeam, Resolved, Closed];

    private TicketStatus(string value)
    {
        Value = value;
    }

    public static TicketStatus Create(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required", nameof(status));
        }

        return status.Trim() switch
        {
            "New" => New,
            "Open" => Open,
            "Assigned" => Assigned,
            "In Progress" => InProgress,
            "Waiting for Customer" => WaitingForCustomer,
            "Waiting for Internal Team" => WaitingForInternalTeam,
            "Resolved" => Resolved,
            "Closed" => Closed,
            _ => throw new ArgumentException(
                $"Invalid ticket status: {status}. Must be one of the eight lifecycle statuses (AC-501).",
                nameof(status))
        };
    }

    public static bool TryCreate(string? status, out TicketStatus? result, out string? error)
    {
        try
        {
            result = Create(status);
            error = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            result = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// The transition table (AC-501). Everything not listed is refused, and the diagonal is
    /// deliberately empty — a ticket cannot transition to a status it already holds (AC-39/AC-502).
    /// </summary>
    public bool CanTransitionTo(TicketStatus target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return (Value, target.Value) switch
        {
            ("New", "Open") => true,
            ("Open", "Assigned") => true,
            ("Open", "Resolved") => true,
            ("Assigned", "In Progress") => true,
            ("In Progress", "Waiting for Customer") => true,
            ("In Progress", "Waiting for Internal Team") => true,
            ("In Progress", "Resolved") => true,
            ("Waiting for Customer", "In Progress") => true,
            ("Waiting for Internal Team", "In Progress") => true,
            ("Resolved", "In Progress") => true,   // reopen, AC-503
            ("Resolved", "Closed") => true,
            ("Closed", "In Progress") => true,     // reopen, AC-503
            _ => false
        };
    }

    /// <summary>
    /// True when moving to <paramref name="target"/> is a reopen rather than ordinary progress —
    /// which history records under its own change type (AC-503). Uses <see cref="Value"/> equality
    /// rather than reference identity so the check is reliable for any two created instances.
    /// </summary>
    public bool IsReopenTo(TicketStatus target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Value == InProgress.Value
            && (Value == Resolved.Value || Value == Closed.Value);
    }

    /// <summary>A work state requires an assignee (AC-505).</summary>
    public bool IsWorkState() =>
        Value is "In Progress" or "Waiting for Customer" or "Waiting for Internal Team";

    public bool IsNew => Value == New.Value;
    public bool IsOpen => Value == Open.Value;
    public bool IsAssigned => Value == Assigned.Value;
    public bool IsInProgress => Value == InProgress.Value;
    public bool IsWaitingForCustomer => Value == WaitingForCustomer.Value;
    public bool IsWaitingForInternalTeam => Value == WaitingForInternalTeam.Value;
    public bool IsResolved => Value == Resolved.Value;
    public bool IsClosed => Value == Closed.Value;

    public static implicit operator string(TicketStatus status) => status.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
