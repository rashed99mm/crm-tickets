namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// How a ticket was resolved (US-922, AC-922.2). Five values fixed by the FEAT-32 spec; persisted
/// as a string for the same reason as <see cref="TicketStatus"/>.
/// </summary>
public sealed class TicketResolutionCode : ValueObject
{
    public string Value { get; }

    public static readonly TicketResolutionCode Fixed = new("Fixed");
    public static readonly TicketResolutionCode Workaround = new("Workaround");
    public static readonly TicketResolutionCode Duplicate = new("Duplicate");
    public static readonly TicketResolutionCode CannotReproduce = new("CannotReproduce");
    public static readonly TicketResolutionCode NoResponse = new("NoResponse");

    public static IReadOnlyList<TicketResolutionCode> All { get; } =
        [Fixed, Workaround, Duplicate, CannotReproduce, NoResponse];

    private TicketResolutionCode(string value)
    {
        Value = value;
    }

    public static TicketResolutionCode Create(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A resolution code is required", nameof(code));
        }

        return code.Trim() switch
        {
            "Fixed" => Fixed,
            "Workaround" => Workaround,
            "Duplicate" => Duplicate,
            "CannotReproduce" => CannotReproduce,
            "NoResponse" => NoResponse,
            _ => throw new ArgumentException(
                $"Invalid resolution code: {code}. Must be Fixed, Workaround, Duplicate, CannotReproduce, or NoResponse.",
                nameof(code))
        };
    }

    public static bool TryCreate(string? code, out TicketResolutionCode? result, out string? error)
    {
        try
        {
            result = Create(code);
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

    public static implicit operator string(TicketResolutionCode code) => code.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
