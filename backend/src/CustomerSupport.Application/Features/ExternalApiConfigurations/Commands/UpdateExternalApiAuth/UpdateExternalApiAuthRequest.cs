namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.UpdateExternalApiAuth;

public record UpdateExternalApiAuthRequest(
    string AuthType,
    string? AuthKeyName,
    string? AuthKeyLocation,
    string? AuthValue,
    string? AuthToken,
    string? AuthTokenUrl,
    string? AuthClientId,
    string? AuthClientSecret,
    string? AuthScope,
    bool AuthAutoRefresh
);
