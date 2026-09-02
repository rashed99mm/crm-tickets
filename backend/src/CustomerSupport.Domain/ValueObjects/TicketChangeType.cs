namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// What a <c>TicketHistory</c> row records (AC-48). Six values: a ticket is created, assigned,
/// reassigned, moved along the lifecycle, reopened, or escalated (US-218, AC-218.1).
/// </summary>
public sealed class TicketChangeType : ValueObject
{
    public string Value { get; }

    public static readonly TicketChangeType Created = new("Created");
    public static readonly TicketChangeType Assigned = new("Assigned");
    public static readonly TicketChangeType Reassigned = new("Reassigned");
    public static readonly TicketChangeType StatusChanged = new("StatusChanged");
    public static readonly TicketChangeType Reopened = new("Reopened");
    public static readonly TicketChangeType Escalated = new("Escalated");
    public static readonly TicketChangeType Reprioritized = new("Reprioritized");
    public static readonly TicketChangeType TagAdded = new("TagAdded");
    public static readonly TicketChangeType TagRemoved = new("TagRemoved");

    public static IReadOnlyList<TicketChangeType> All { get; } =
        [Created, Assigned, Reassigned, StatusChanged, Reopened, Escalated, Reprioritized, TagAdded, TagRemoved];

    private TicketChangeType(string value)
    {
        Value = value;
    }

    public static TicketChangeType Create(string? changeType)
    {
        if (string.IsNullOrWhiteSpace(changeType))
        {
            throw new ArgumentException("Change type is required", nameof(changeType));
        }

        return changeType.Trim() switch
        {
            "Created" => Created,
            "Assigned" => Assigned,
            "Reassigned" => Reassigned,
            "StatusChanged" => StatusChanged,
            "Reopened" => Reopened,
            "Escalated" => Escalated,
            "Reprioritized" => Reprioritized,
            "TagAdded" => TagAdded,
            "TagRemoved" => TagRemoved,
            _ => throw new ArgumentException(
                $"Invalid ticket change type: {changeType}. Must be Created, Assigned, Reassigned, StatusChanged, Reopened, Escalated, Reprioritized, TagAdded, or TagRemoved.",
                nameof(changeType))
        };
    }

    public static implicit operator string(TicketChangeType changeType) => changeType.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
