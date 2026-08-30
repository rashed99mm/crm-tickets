using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Dtos;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.ExternalApis;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Queries.GetExternalApiConfigurationByName;

public class GetExternalApiConfigurationByNameQueryHandler : IQueryHandler<GetExternalApiConfigurationByNameQuery, Response<ExternalApiConfigurationDto>>
{
    private readonly IRepository<ExternalApiConfiguration> _repository;
    private readonly IMessageFactory _messages;
    private readonly ILogger<GetExternalApiConfigurationByNameQueryHandler> _logger;

    public GetExternalApiConfigurationByNameQueryHandler(
        IRepository<ExternalApiConfiguration> repository,
        IMessageFactory messages,
        ILogger<GetExternalApiConfigurationByNameQueryHandler> logger)
    {
        _repository = repository;
        _messages = messages;
        _logger = logger;
    }

    public async Task<Response<ExternalApiConfigurationDto>> Handle(GetExternalApiConfigurationByNameQuery request, CancellationToken ct)
    {
        _logger.LogInformation("Retrieving external API config {ApiName}", request.Name);

        var entity = await _repository.FirstOrDefaultAsync(c => c.Name == request.Name, ct);
        if (entity == null)
        {
            _logger.LogWarning("External API config {ApiName} not found", request.Name);
            return _messages.Fail<ExternalApiConfigurationDto>(
                ApplicationErrors.ExternalApi.NOT_FOUND,
                MessageType.NotFound);
        }

        return _messages.Success(MapToDto(entity), "ExternalApiConfiguration.Detail");
    }

    private static ExternalApiConfigurationDto MapToDto(ExternalApiConfiguration entity) => new(
        entity.Id,
        entity.Name,
        entity.BaseUrl,
        entity.TimeoutSeconds,
        entity.IsEnabled,
        entity.AuthType,
        entity.AuthKeyName,
        entity.AuthKeyLocation,
        entity.AuthTokenUrl,
        entity.AuthClientId,
        entity.AuthScope,
        entity.AuthAutoRefresh,
        entity.CreatedAt,
        entity.UpdatedAt
    );
}
