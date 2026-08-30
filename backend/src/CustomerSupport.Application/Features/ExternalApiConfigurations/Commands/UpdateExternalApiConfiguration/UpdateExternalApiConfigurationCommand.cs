using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.UpdateExternalApiConfiguration;

public record UpdateExternalApiConfigurationCommand(
    string Name,
    string? BaseUrl,
    int? TimeoutSeconds
) : ICommand<Response<Guid>>;
