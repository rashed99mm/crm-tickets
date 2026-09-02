namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// How two tickets relate (US-925). <c>RelatedTo</c> is displayed symmetrically;
/// <c>DuplicateOf</c> is directional — the source is the duplicate, the target the original.
/// </summary>
public sealed class TicketLinkType : ValueObject
{
    public string Value { get; }

    public static readonly TicketLinkType RelatedTo = new("RelatedTo");
    public static readonly TicketLinkType DuplicateOf = new("DuplicateOf");

    public static IReadOnlyList<TicketLinkType> All { get; } = [RelatedTo, DuplicateOf];

    private TicketLinkType(string value)
    {
        Value = value;
    }

    public static TicketLinkType Create(string? linkType)
    {
        if (string.IsNullOrWhiteSpace(linkType))
        {
            throw new ArgumentException("A link type is required", nameof(linkType));
        }

        return linkType.Trim() switch
        {
            "RelatedTo" => RelatedTo,
            "DuplicateOf" => DuplicateOf,
            _ => throw new ArgumentException(
                $"Invalid ticket link type: {linkType}. Must be RelatedTo or DuplicateOf.", nameof(linkType))
        };
    }

    public static bool TryCreate(string? linkType, out TicketLinkType? result, out string? error)
    {
        try
        {
            result = Create(linkType);
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

    public static implicit operator string(TicketLinkType linkType) => linkType.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
