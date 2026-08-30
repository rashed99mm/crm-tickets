namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Dtos;

public record ExternalApiConfigurationDto(
    Guid Id,
    string Name,
    string BaseUrl,
    int TimeoutSeconds,
    bool IsEnabled,
    string AuthType,
    string? AuthKeyName,
    string? AuthKeyLocation,
    string? AuthTokenUrl,
    string? AuthClientId,
    string? AuthScope,
    bool AuthAutoRefresh,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
