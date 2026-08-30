using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;

using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.PlatformSettings;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.PlatformSettings.Commands.UpdatePlatformSetting;

/// <summary>
/// Updates an existing platform setting, handling encryption changes.
/// </summary>
public class UpdatePlatformSettingCommandHandler : ICommandHandler<UpdatePlatformSettingCommand, Response<Guid>>
{
    private readonly IRepository<PlatformSetting> _settingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly IMessageFactory _messages;
    private readonly ILogger<UpdatePlatformSettingCommandHandler> _logger;

    public UpdatePlatformSettingCommandHandler(
        IRepository<PlatformSetting> settingRepository,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        IMessageFactory messages,
        ILogger<UpdatePlatformSettingCommandHandler> logger)
    {
        _settingRepository = settingRepository;
        _unitOfWork = unitOfWork;
        _secretProtector = secretProtector;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the update platform setting command.
    /// </summary>
    /// <param name="request">Platform setting update details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the updated setting identifier or a localized error.</returns>
    public async Task<Response<Guid>> Handle(UpdatePlatformSettingCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Updating platform setting {SettingId}", request.Id);

        var setting = await _settingRepository.GetByIdAsync(request.Id, ct);
        if (setting == null)
        {
            _logger.LogWarning("Update failed — platform setting {SettingId} not found", request.Id);
            return _messages.Fail<Guid>(
                ApplicationErrors.PlatformSetting.NOT_FOUND,
                MessageType.NotFound);
        }

        var targetIsEncrypted = request.IsEncrypted ?? setting.IsEncrypted;

        if (!string.IsNullOrEmpty(request.Value))
        {
            setting.Value = targetIsEncrypted
                ? _secretProtector.Protect(request.Value)
                : request.Value;
        }
        else if (request.IsEncrypted.HasValue && request.IsEncrypted.Value != setting.IsEncrypted)
        {
            string currentValue;
            try
            {
                currentValue = setting.IsEncrypted
                    ? _secretProtector.Unprotect(setting.Value)
                    : setting.Value;
            }
            catch
            {
                _logger.LogError("Re-protection failed for platform setting {SettingId}", request.Id);
                return _messages.Fail<Guid>(
                    ApplicationErrors.PlatformSetting.REPROTECT_FAILED,
                    MessageType.Internal);
            }

            setting.Value = request.IsEncrypted.Value
                ? _secretProtector.Protect(currentValue)
                : currentValue;
        }

        if (request.Description != null) setting.Description = request.Description;
        if (request.IsEncrypted.HasValue) setting.IsEncrypted = request.IsEncrypted.Value;
        if (request.IsPublic.HasValue) setting.IsPublic = request.IsPublic.Value;

        _settingRepository.Update(setting);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Platform setting {SettingId} updated successfully", setting.Id);

        return _messages.Success(setting.Id, ApplicationErrors.General.SUCCESS_UPDATED);
    }
}
