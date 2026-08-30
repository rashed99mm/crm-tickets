using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.DisableExternalApiConfiguration;

public record DisableExternalApiConfigurationCommand(string Name) : ICommand<Response<Guid>>;
