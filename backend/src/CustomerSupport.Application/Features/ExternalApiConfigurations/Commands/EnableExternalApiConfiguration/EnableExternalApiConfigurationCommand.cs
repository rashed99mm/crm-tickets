using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.EnableExternalApiConfiguration;

public record EnableExternalApiConfigurationCommand(string Name) : ICommand<Response<Guid>>;
