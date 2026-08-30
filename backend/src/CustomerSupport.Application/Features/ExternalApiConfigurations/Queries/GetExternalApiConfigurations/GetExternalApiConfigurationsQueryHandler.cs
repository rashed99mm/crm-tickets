using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Dtos;
using CustomerSupport.Domain.Entities.ExternalApis;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Queries.GetExternalApiConfigurations;

public class GetExternalApiConfigurationsQueryHandler : IQueryHandler<GetExternalApiConfigurationsQuery, Response<PaginatedList<ExternalApiConfigurationDto>>>
{
    private readonly IRepository<ExternalApiConfiguration> _repository;
    private readonly IMessageFactory _messages;

    public GetExternalApiConfigurationsQueryHandler(IRepository<ExternalApiConfiguration> repository, IMessageFactory messages)
    {
        _repository = repository;
        _messages = messages;
    }

    public async Task<Response<PaginatedList<ExternalApiConfigurationDto>>> Handle(GetExternalApiConfigurationsQuery request, CancellationToken ct)
    {
        var filter = PredicateBuilder.True<ExternalApiConfiguration>()
            .WhereIf(!string.IsNullOrWhiteSpace(request.Search),
                c => c.Name.Contains(request.Search) || c.BaseUrl.Contains(request.Search));

        var result = await _repository.GetPagedAsync<ExternalApiConfigurationDto>(request, filter, ct);
        return _messages.Success(result, "ExternalApiConfiguration.List");
    }
}
