namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// The tag normalization rule (US-924, spec A6), stated once: trim, collapse internal whitespace,
/// invariant lowercase; 1-30 chars; Unicode letters (Arabic included), digits, dash and space.
/// A static rule rather than a wrapping value object because the persisted thing is the entity
/// (<c>TicketTag</c>) — this is the rule it must pass through, not a second identity.
/// </summary>
public static class TagValue
{
    public const int MaxLength = 30;
    public const int MaxPerTicket = 10;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A tag value is required", nameof(value));
        }

        var collapsed = string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var normalized = collapsed.ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new ArgumentException($"A tag must not exceed {MaxLength} characters", nameof(value));
        }

        if (!normalized.All(c => char.IsLetterOrDigit(c) || c is '-' or ' '))
        {
            throw new ArgumentException("A tag may contain only letters, digits, dashes and spaces", nameof(value));
        }

        return normalized;
    }
}
