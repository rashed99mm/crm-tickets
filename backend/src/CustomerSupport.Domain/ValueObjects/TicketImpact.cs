namespace CustomerSupport.Domain.ValueObjects;

/// <summary>How widely the incident hurts (US-923). One axis of the priority matrix.</summary>
public sealed class TicketImpact : ValueObject
{
    public string Value { get; }

    public static readonly TicketImpact Low = new("Low");
    public static readonly TicketImpact Medium = new("Medium");
    public static readonly TicketImpact High = new("High");

    public static IReadOnlyList<TicketImpact> All { get; } = [Low, Medium, High];

    private TicketImpact(string value)
    {
        Value = value;
    }

    public static TicketImpact Create(string? impact)
    {
        if (string.IsNullOrWhiteSpace(impact))
        {
            throw new ArgumentException("Impact is required", nameof(impact));
        }

        return impact.Trim() switch
        {
            "Low" => Low,
            "Medium" => Medium,
            "High" => High,
            _ => throw new ArgumentException(
                $"Invalid ticket impact: {impact}. Must be Low, Medium, or High.", nameof(impact))
        };
    }

    public static bool TryCreate(string? impact, out TicketImpact? result, out string? error)
    {
        try
        {
            result = Create(impact);
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

    public static implicit operator string(TicketImpact impact) => impact.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
