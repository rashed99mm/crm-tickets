namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.CreateExternalApiConfiguration;

public record CreateExternalApiConfigurationRequest(
    string Name,
    string BaseUrl,
    int TimeoutSeconds,
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
