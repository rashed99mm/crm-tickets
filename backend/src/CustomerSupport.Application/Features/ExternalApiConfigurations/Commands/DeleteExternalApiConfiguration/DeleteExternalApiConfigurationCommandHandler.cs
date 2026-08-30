using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.ExternalApis;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.DeleteExternalApiConfiguration;

public class DeleteExternalApiConfigurationCommandHandler : ICommandHandler<DeleteExternalApiConfigurationCommand, Response<Unit>>
{
    private readonly IRepository<ExternalApiConfiguration> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExternalApiConfigurationProvider _provider;
    private readonly IMessageFactory _messages;
    private readonly ILogger<DeleteExternalApiConfigurationCommandHandler> _logger;

    public DeleteExternalApiConfigurationCommandHandler(
        IRepository<ExternalApiConfiguration> repository,
        IUnitOfWork unitOfWork,
        IExternalApiConfigurationProvider provider,
        IMessageFactory messages,
        ILogger<DeleteExternalApiConfigurationCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _provider = provider;
        _messages = messages;
        _logger = logger;
    }

    public async Task<Response<Unit>> Handle(DeleteExternalApiConfigurationCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Deleting external API config {ApiName}", request.Name);

        var entity = await _repository.FirstOrDefaultAsync(c => c.Name == request.Name, ct);
        if (entity == null)
        {
            _logger.LogWarning("External API config {ApiName} not found", request.Name);
            return _messages.Fail<Unit>(
                ApplicationErrors.ExternalApi.NOT_FOUND,
                MessageType.NotFound);
        }

        entity.SoftDelete();
        _repository.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        await _provider.ReloadAsync(ct);

        _logger.LogInformation("External API config {ApiName} deleted", entity.Name);

        return _messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_DELETED);
    }
}
