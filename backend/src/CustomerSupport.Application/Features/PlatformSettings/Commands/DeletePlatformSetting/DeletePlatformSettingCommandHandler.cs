using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;

using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.PlatformSettings;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.PlatformSettings.Commands.DeletePlatformSetting;

/// <summary>
/// Soft-deletes a platform setting.
/// </summary>
public class DeletePlatformSettingCommandHandler : ICommandHandler<DeletePlatformSettingCommand, Response<Unit>>
{
    private readonly IRepository<PlatformSetting> _settingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageFactory _messages;
    private readonly ILogger<DeletePlatformSettingCommandHandler> _logger;

    public DeletePlatformSettingCommandHandler(IRepository<PlatformSetting> settingRepository, IUnitOfWork unitOfWork, IMessageFactory messages, ILogger<DeletePlatformSettingCommandHandler> logger)
    {
        _settingRepository = settingRepository;
        _unitOfWork = unitOfWork;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the delete platform setting command.
    /// </summary>
    /// <param name="request">The setting identifier to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success result or a localized error.</returns>
    public async Task<Response<Unit>> Handle(DeletePlatformSettingCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Deleting platform setting {SettingId}", request.Id);

        var setting = await _settingRepository.GetByIdAsync(request.Id, ct);
        if (setting == null)
        {
            _logger.LogWarning("Delete failed — platform setting {SettingId} not found", request.Id);
            return _messages.Fail<Unit>(
                ApplicationErrors.PlatformSetting.NOT_FOUND,
                MessageType.NotFound);
        }

        setting.SoftDelete();
        _settingRepository.Update(setting);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Platform setting {SettingId} deleted successfully", request.Id);

        return _messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_DELETED);
    }
}
