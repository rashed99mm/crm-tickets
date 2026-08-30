using System.Text.RegularExpressions;

namespace CustomerSupport.Domain.Entities.Customers;

/// <summary>
/// The person who contacted us (AC-7..AC-16).
///
/// Uniqueness of <see cref="Email"/> is not enforced here — it is a filtered unique index, because
/// only the database can answer it without a race (AC-9, ADR-0006). What the entity guarantees is
/// that the value reaching that index is normalised.
/// </summary>
public partial class Customer : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;

    /// <summary>Stored lower-cased, so <c>UX_Customers_Email</c> catches case variants too.</summary>
    public string Email { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    /// <summary>Organisational grouping (FEAT-16, AC-117). Nullable and unset — see Ticket.DepartmentId.</summary>
    public Guid? BranchId { get; private set; }

    public static Customer Create(string name, string email, string? phone)
    {
        var (validName, validEmail) = Validate(name, email);

        return new Customer
        {
            Id = Guid.NewGuid(),
            Name = validName,
            Email = validEmail,
            Phone = Normalise(phone),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Corrects a record. Validation is the same as creation's — AC-14 says so explicitly.</summary>
    public void Update(string name, string email, string? phone)
    {
        var (validName, validEmail) = Validate(name, email);

        Name = validName;
        Email = validEmail;
        Phone = Normalise(phone);
        MarkUpdated();
    }

    /// <summary>Associates the customer with the branch that owns the interaction.</summary>
    public void AssignBranch(Guid? branchId)
    {
        BranchId = branchId;
        MarkUpdated();
    }

    private static (string Name, string Email) Validate(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        if (name.Length > 200)
        {
            throw new ArgumentException("Name must not exceed 200 characters", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required", nameof(email));
        }

        var trimmedEmail = email.Trim();

        if (trimmedEmail.Length > 320)
        {
            throw new ArgumentException("Email must not exceed 320 characters", nameof(email));
        }

        if (!EmailPattern().IsMatch(trimmedEmail))
        {
            throw new ArgumentException($"Invalid email address: {email}", nameof(email));
        }

        return (name.Trim(), trimmedEmail.ToLowerInvariant());
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Deliberately not RFC 5322. It rejects the shapes a support agent actually mistypes — a
    /// missing local part, a missing dotted domain, an embedded space — and does not attempt to
    /// adjudicate the exotic addresses that only a delivery attempt can settle.
    /// </summary>
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
