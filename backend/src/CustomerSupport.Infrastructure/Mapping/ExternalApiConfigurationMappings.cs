using AutoMapper;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Dtos;
using CustomerSupport.Domain.Entities.ExternalApis;

namespace CustomerSupport.Infrastructure.Mapping;

public class ExternalApiConfigurationMappings : Profile
{
    public ExternalApiConfigurationMappings()
    {
        CreateMap<ExternalApiConfiguration, ExternalApiConfigurationDto>();
    }
}
