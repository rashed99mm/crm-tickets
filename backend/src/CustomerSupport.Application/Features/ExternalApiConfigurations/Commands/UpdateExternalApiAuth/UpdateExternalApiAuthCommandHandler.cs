using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.ExternalApis;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.UpdateExternalApiAuth;

public class UpdateExternalApiAuthCommandHandler : ICommandHandler<UpdateExternalApiAuthCommand, Response<Guid>>
{
    private readonly IRepository<ExternalApiConfiguration> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly IExternalApiConfigurationProvider _provider;
    private readonly IMessageFactory _messages;
    private readonly ILogger<UpdateExternalApiAuthCommandHandler> _logger;

    public UpdateExternalApiAuthCommandHandler(
        IRepository<ExternalApiConfiguration> repository,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        IExternalApiConfigurationProvider provider,
        IMessageFactory messages,
        ILogger<UpdateExternalApiAuthCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _secretProtector = secretProtector;
        _provider = provider;
        _messages = messages;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(UpdateExternalApiAuthCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Updating auth for external API config {ApiName}", request.Name);

        var entity = await _repository.FirstOrDefaultAsync(c => c.Name == request.Name, ct);
        if (entity == null)
        {
            _logger.LogWarning("External API config {ApiName} not found", request.Name);
            return _messages.Fail<Guid>(
                ApplicationErrors.ExternalApi.NOT_FOUND,
                MessageType.NotFound);
        }

        entity.UpdateAuth(
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

        _repository.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        await _provider.ReloadAsync(ct);

        _logger.LogInformation("Auth updated for external API config {ApiName}", entity.Name);

        return _messages.Success(entity.Id, ApplicationErrors.General.SUCCESS_UPDATED);
    }

    private string? ProtectIfNotEmpty(string? value)
    {
        return !string.IsNullOrEmpty(value) ? _secretProtector.Protect(value) : value;
    }
}
