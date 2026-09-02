namespace CustomerSupport.Domain.ValueObjects;

/// <summary>How fast the incident degrades (US-923). One axis of the priority matrix.</summary>
public sealed class TicketUrgency : ValueObject
{
    public string Value { get; }

    public static readonly TicketUrgency Low = new("Low");
    public static readonly TicketUrgency Medium = new("Medium");
    public static readonly TicketUrgency High = new("High");

    public static IReadOnlyList<TicketUrgency> All { get; } = [Low, Medium, High];

    private TicketUrgency(string value)
    {
        Value = value;
    }

    public static TicketUrgency Create(string? urgency)
    {
        if (string.IsNullOrWhiteSpace(urgency))
        {
            throw new ArgumentException("Urgency is required", nameof(urgency));
        }

        return urgency.Trim() switch
        {
            "Low" => Low,
            "Medium" => Medium,
            "High" => High,
            _ => throw new ArgumentException(
                $"Invalid ticket urgency: {urgency}. Must be Low, Medium, or High.", nameof(urgency))
        };
    }

    public static bool TryCreate(string? urgency, out TicketUrgency? result, out string? error)
    {
        try
        {
            result = Create(urgency);
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

    public static implicit operator string(TicketUrgency urgency) => urgency.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
