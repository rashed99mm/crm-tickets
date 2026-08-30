using System.Security.Claims;

namespace CustomerSupport.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles, IEnumerable<Claim>? additionalClaims = null);
    string GenerateRefreshToken();
    DateTime GetTokenExpiration(string token);
    Guid? GetUserIdFromToken(string token);
}
