using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Dtos;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Queries.GetExternalApiConfigurationByName;

public record GetExternalApiConfigurationByNameQuery(string Name) : IQuery<Response<ExternalApiConfigurationDto>>;
