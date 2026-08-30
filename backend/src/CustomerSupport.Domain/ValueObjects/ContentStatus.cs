namespace CustomerSupport.Domain.ValueObjects;

public sealed class ContentStatus : ValueObject
{
    public string Value { get; }

    public static readonly ContentStatus Draft = new("Draft");
    public static readonly ContentStatus Published = new("Published");
    public static readonly ContentStatus Archived = new("Archived");

    private ContentStatus(string value)
    {
        Value = value;
    }

    public static ContentStatus Create(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required", nameof(status));
        }

        return status.Trim() switch
        {
            "Draft" => Draft,
            "Published" => Published,
            "Archived" => Archived,
            _ => throw new ArgumentException($"Invalid content status: {status}. Must be Draft, Published, or Archived.", nameof(status))
        };
    }

    public static bool TryCreate(string? status, out ContentStatus? result, out string? error)
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

    public bool CanTransitionTo(ContentStatus target)
    {
        return (Value, target.Value) switch
        {
            ("Draft", "Published") => true,
            ("Draft", "Archived") => true,
            ("Published", "Archived") => true,
            _ => false
        };
    }

    public bool IsDraft => this == Draft;
    public bool IsPublished => this == Published;
    public bool IsArchived => this == Archived;

    public static implicit operator string(ContentStatus status) => status.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
