namespace CustomerSupport.Application.Features.Auth;

/// <summary>
/// Well-known claim types this platform issues, kept in one place so handlers and the controllers
/// that read them cannot drift (US-402/US-403, PJ-3/4).
/// </summary>
public static class AuthClaimTypes
{
    /// <summary>The linked customer's id, present only on portal accounts (US-401).</summary>
    public const string CustomerId = "customerId";
}