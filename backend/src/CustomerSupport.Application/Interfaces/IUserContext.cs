using System.Globalization;

namespace CustomerSupport.Application.Interfaces;

public interface IUserContext
{
    Guid UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    CultureInfo Locale { get; }
    string? GetClaim(string claimType);
    IEnumerable<string> GetRoles();
    bool HasRole(string role);
    bool HasAnyRole(params string[] roles);
}
