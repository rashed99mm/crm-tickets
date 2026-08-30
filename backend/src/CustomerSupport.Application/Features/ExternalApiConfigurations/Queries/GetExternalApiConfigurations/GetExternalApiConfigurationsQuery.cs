using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Dtos;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Queries.GetExternalApiConfigurations;

public class GetExternalApiConfigurationsQuery : BasePagedQuery, IQuery<Response<PaginatedList<ExternalApiConfigurationDto>>>
{
    public string? Search { get; init; }
}
