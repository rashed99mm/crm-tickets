using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.DeleteExternalApiConfiguration;

public record DeleteExternalApiConfigurationCommand(string Name) : ICommand<Response<Unit>>;
