using System.Security.Claims;

namespace CustomerSupport.Api.Shared.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("Authenticated user id claim is missing or invalid.");
    }

    /// <summary>
    /// The portal <c>customerId</c> claim, if the signed-in account is a linked portal customer
    /// (US-401/US-402). Staff accounts have no such claim, and a request that reaches a portal-only
    /// endpoint without it is a 403, not a 404 — see <c>PortalController</c>.
    /// </summary>
    public static Guid? GetCustomerId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(CustomerSupport.Application.Features.Auth.AuthClaimTypes.CustomerId)?.Value;
        return Guid.TryParse(value, out var customerId) ? customerId : null;
    }
}
