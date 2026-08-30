namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// Ticket urgency. The BRD discusses priority without ever enumerating it; these four values are
/// fixed by <c>docs/architecture/erd.md</c> §6 and an SLA policy conversation could still revise
/// them. Persisted as a string for the same reason as <see cref="TicketStatus"/>.
/// </summary>
public sealed class TicketPriority : ValueObject
{
    public string Value { get; }

    public static readonly TicketPriority Low = new("Low");
    public static readonly TicketPriority Normal = new("Normal");
    public static readonly TicketPriority High = new("High");
    public static readonly TicketPriority Urgent = new("Urgent");

    /// <summary>Every priority, least urgent first.</summary>
    public static IReadOnlyList<TicketPriority> All { get; } = [Low, Normal, High, Urgent];

    private TicketPriority(string value)
    {
        Value = value;
    }

    public static TicketPriority Create(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException("Priority is required", nameof(priority));
        }

        return priority.Trim() switch
        {
            "Low" => Low,
            "Normal" => Normal,
            "High" => High,
            "Urgent" => Urgent,
            _ => throw new ArgumentException(
                $"Invalid ticket priority: {priority}. Must be Low, Normal, High, or Urgent.",
                nameof(priority))
        };
    }

    public static bool TryCreate(string? priority, out TicketPriority? result, out string? error)
    {
        try
        {
            result = Create(priority);
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

    public static implicit operator string(TicketPriority priority) => priority.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
