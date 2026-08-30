namespace CustomerSupport.Application.Features.Auth.Dtos;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(
    string Email,
    string Username,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber = null);

public record RefreshTokenRequest(string AccessToken, string RefreshToken);

public record LogoutRequest(string? RefreshToken);

/// <summary>
/// No user identifier travels in this request. The caller's id comes from the
/// authenticated token, never from the body — a body cannot ask to change someone else's
/// password.
/// </summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
