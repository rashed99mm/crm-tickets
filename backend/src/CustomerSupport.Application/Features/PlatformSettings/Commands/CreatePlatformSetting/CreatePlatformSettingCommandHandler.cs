using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;

using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.PlatformSettings;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.PlatformSettings.Commands.CreatePlatformSetting;

/// <summary>
/// Creates a new platform setting with optional encryption.
/// </summary>
public class CreatePlatformSettingCommandHandler : ICommandHandler<CreatePlatformSettingCommand, Response<Guid>>
{
    private readonly IRepository<PlatformSetting> _settingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly IMessageFactory _messages;
    private readonly ILogger<CreatePlatformSettingCommandHandler> _logger;

    public CreatePlatformSettingCommandHandler(
        IRepository<PlatformSetting> settingRepository,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        IMessageFactory messages,
        ILogger<CreatePlatformSettingCommandHandler> logger)
    {
        _settingRepository = settingRepository;
        _unitOfWork = unitOfWork;
        _secretProtector = secretProtector;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the create platform setting command.
    /// </summary>
    /// <param name="request">Platform setting creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the created setting identifier or a localized error.</returns>
    public async Task<Response<Guid>> Handle(CreatePlatformSettingCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Creating platform setting with key {SettingKey}", request.Key);

        var settingExists = await _settingRepository.ExistsAsync(s => s.Key == request.Key, ct);
        if (settingExists)
        {
            _logger.LogWarning("Platform setting creation failed — key {SettingKey} already exists", request.Key);
            return _messages.Fail<Guid>(
                ApplicationErrors.PlatformSetting.ALREADY_EXISTS,
                MessageType.Conflict);
        }

        var setting = new PlatformSetting
        {
            Id = Guid.NewGuid(),
            Key = request.Key,
            Value = request.IsEncrypted ? _secretProtector.Protect(request.Value) : request.Value,
            Description = request.Description,
            Category = request.Category,
            ValueType = request.ValueType,
            IsEncrypted = request.IsEncrypted,
            IsPublic = request.IsPublic
        };

        await _settingRepository.AddAsync(setting, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Platform setting {SettingId} with key {SettingKey} created successfully", setting.Id, request.Key);

        return _messages.Success(setting.Id, ApplicationErrors.General.SUCCESS_CREATED);
    }
}
