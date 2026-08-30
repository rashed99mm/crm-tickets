namespace CustomerSupport.Domain.ValueObjects;

public sealed class Email : ValueObject
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static Email Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required", nameof(email));
        }

        email = email.Trim();

        if (email.Length > 255)
        {
            throw new ArgumentException("Email must not exceed 255 characters", nameof(email));
        }

        if (!email.Contains('@') || email.Count(c => c == '@') != 1)
        {
            throw new ArgumentException("Invalid email format", nameof(email));
        }

        var parts = email.Split('@');
        if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException("Invalid email format", nameof(email));
        }

        if (!IsValidDomain(parts[1]))
        {
            throw new ArgumentException("Invalid email domain", nameof(email));
        }

        return new Email(email);
    }

    public static bool TryCreate(string? email, out Email? result, out string? error)
    {
        try
        {
            result = Create(email);
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

    private static bool IsValidDomain(string domain)
    {
        if (domain.Length > 253)
            return false;

        if (domain.StartsWith('.') || domain.EndsWith('.'))
            return false;

        return true;
    }

    public static implicit operator string(Email email) => email.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
