using Microsoft.AspNetCore.Identity;

namespace CustomerSupport.Domain.Entities.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    public void SetDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public static ApplicationRole Create(string name, string? description = null)
    {
        return new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static class Roles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string ContentManager = "ContentManager";
        public const string StateRepresentative = "StateRepresentative";
        public const string User = "User";
        public const string Visitor = "Visitor";

        // The support domain's two roles (slice assumption A2). Added alongside the inherited six
        // rather than replacing them - see ADR-0012 for why the rename was rejected and what the
        // resulting two-vocabulary cost is.
        public const string Agent = "Agent";
        public const string Supervisor = "Supervisor";
    }
}
