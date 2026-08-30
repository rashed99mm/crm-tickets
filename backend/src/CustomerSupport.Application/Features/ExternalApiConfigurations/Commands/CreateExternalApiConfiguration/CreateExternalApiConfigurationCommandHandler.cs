using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.ExternalApis;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.CreateExternalApiConfiguration;

public class CreateExternalApiConfigurationCommandHandler : ICommandHandler<CreateExternalApiConfigurationCommand, Response<Guid>>
{
    private readonly IRepository<ExternalApiConfiguration> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly IExternalApiConfigurationProvider _provider;
    private readonly IMessageFactory _messages;
    private readonly ILogger<CreateExternalApiConfigurationCommandHandler> _logger;

    public CreateExternalApiConfigurationCommandHandler(
        IRepository<ExternalApiConfiguration> repository,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        IExternalApiConfigurationProvider provider,
        IMessageFactory messages,
        ILogger<CreateExternalApiConfigurationCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _secretProtector = secretProtector;
        _provider = provider;
        _messages = messages;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(CreateExternalApiConfigurationCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Creating external API config {ApiName}", request.Name);

        var exists = await _repository.ExistsAsync(c => c.Name == request.Name, ct);
        if (exists)
        {
            _logger.LogWarning("External API config {ApiName} already exists", request.Name);
            return _messages.Fail<Guid>(
                ApplicationErrors.ExternalApi.ALREADY_EXISTS,
                MessageType.Conflict);
        }

        var entity = ExternalApiConfiguration.Create(
            name: request.Name,
            baseUrl: request.BaseUrl,
            timeoutSeconds: request.TimeoutSeconds,
            authType: request.AuthType,
            authKeyName: request.AuthKeyName,
            authKeyLocation: request.AuthKeyLocation,
            authValue: ProtectIfNotEmpty(request.AuthValue),
            authToken: ProtectIfNotEmpty(request.AuthToken),
            authTokenUrl: request.AuthTokenUrl,
            authClientId: ProtectIfNotEmpty(request.AuthClientId),
            authClientSecret: ProtectIfNotEmpty(request.AuthClientSecret),
            authScope: request.AuthScope,
            authAutoRefresh: request.AuthAutoRefresh);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _provider.ReloadAsync(ct);

        _logger.LogInformation("External API config {ApiName} created", entity.Name);

        return _messages.Success(entity.Id, ApplicationErrors.General.SUCCESS_CREATED);
    }

    private string? ProtectIfNotEmpty(string? value)
    {
        return !string.IsNullOrEmpty(value) ? _secretProtector.Protect(value) : value;
    }
}
