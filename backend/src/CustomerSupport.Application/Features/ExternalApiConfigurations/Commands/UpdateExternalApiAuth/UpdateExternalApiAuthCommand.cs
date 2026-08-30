using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.UpdateExternalApiAuth;

public record UpdateExternalApiAuthCommand(
    string Name,
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
) : ICommand<Response<Guid>>;
