using Microsoft.AspNetCore.Identity;

namespace CustomerSupport.Domain.Entities.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? ProfileImageUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>Organisational grouping (FEAT-16, AC-117). Nullable and unset — see Ticket.DepartmentId.</summary>
    public Guid? DepartmentId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? TeamId { get; private set; }

    /// <summary>
    /// The customer this identity belongs to (US-401). Null for staff — only portal registrations
    /// link one. The <c>customerId</c> JWT claim is only issued when this is set (PJ-3/US-402).
    /// </summary>
    public Guid? CustomerId { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public virtual ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    public void UpdateProfile(string? firstName, string? lastName, string? phoneNumber, string? profileImageUrl)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            FirstName = firstName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(lastName))
        {
            LastName = lastName.Trim();
        }

        if (phoneNumber != null)
        {
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        }

        if (profileImageUrl != null)
        {
            ProfileImageUrl = string.IsNullOrWhiteSpace(profileImageUrl) ? null : profileImageUrl.Trim();
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    /// <summary>Links a portal registration to its customer record (US-401). One link per identity;
    /// a second call replaces the previous link rather than accumulating.</summary>
    public void LinkCustomer(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer is required", nameof(customerId));
        }

        CustomerId = customerId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Wires the user into the org drill-down (US-907, AC-511). Admin-managed via the UpdateUser
    /// surface; also used by the seeder so seeded staff sit in the default org.
    /// </summary>
    public void AssignOrganisation(Guid? departmentId, Guid? branchId, Guid? teamId)
    {
        DepartmentId = departmentId;
        BranchId = branchId;
        TeamId = teamId;
        UpdatedAt = DateTime.UtcNow;
    }

    public static ApplicationUser Create(string email, string userName, string firstName, string lastName)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = userName,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        return user;
    }
}
