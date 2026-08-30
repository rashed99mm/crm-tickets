using System.Globalization;
using System.Security.Claims;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Features.Auth.Dtos;
using Microsoft.AspNetCore.Http;

namespace CustomerSupport.Infrastructure.Security;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;
    private HttpContext? HttpContext => _httpContextAccessor.HttpContext;

    public Guid UserId
    {
        get
        {
            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            return Guid.Empty;
        }
    }

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("email")?.Value;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public CultureInfo Locale
    {
        get
        {
            var acceptLanguage = HttpContext?.Request.Headers.AcceptLanguage.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(acceptLanguage))
            {
                var primaryLanguage = acceptLanguage.Split(',').FirstOrDefault()?.Trim().Split(';').FirstOrDefault()?.Trim();
                if (!string.IsNullOrWhiteSpace(primaryLanguage))
                {
                    try
                    {
                        return new CultureInfo(primaryLanguage);
                    }
                    catch (CultureNotFoundException)
                    {
                        // Fall through to default
                    }
                }
            }
            return new CultureInfo("ar-SA");
        }
    }

    public string? GetClaim(string claimType)
    {
        return User?.FindFirst(claimType)?.Value;
    }

    public IEnumerable<string> GetRoles()
    {
        return User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? [];
    }

    public bool HasRole(string role)
    {
        return User?.IsInRole(role) ?? false;
    }

    public bool HasAnyRole(params string[] roles)
    {
        if (User == null) return false;
        return roles.Any(r => User.IsInRole(r));
    }
}
