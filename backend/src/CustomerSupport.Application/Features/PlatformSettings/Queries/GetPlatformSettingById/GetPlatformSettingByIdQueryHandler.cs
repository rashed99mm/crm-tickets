using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;

using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using CustomerSupport.Domain.Entities.PlatformSettings;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.PlatformSettings.Queries.GetPlatformSettingById;

/// <summary>
/// Retrieves a platform setting by unique identifier.
/// </summary>
public class GetPlatformSettingByIdQueryHandler : IQueryHandler<GetPlatformSettingByIdQuery, Response<PlatformSettingDto>>
{
    private readonly IRepository<PlatformSetting> _settingRepository;
    private readonly IMessageFactory _messages;
    private readonly ILogger<GetPlatformSettingByIdQueryHandler> _logger;

    public GetPlatformSettingByIdQueryHandler(IRepository<PlatformSetting> settingRepository, IMessageFactory messages, ILogger<GetPlatformSettingByIdQueryHandler> logger)
    {
        _settingRepository = settingRepository;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the get platform setting by id query.
    /// </summary>
    /// <param name="request">The query request containing the setting identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the setting details or a localized error.</returns>
    public async Task<Response<PlatformSettingDto>> Handle(GetPlatformSettingByIdQuery request, CancellationToken ct)
    {
        _logger.LogInformation("Retrieving platform setting {SettingId}", request.Id);

        var setting = await _settingRepository.GetByIdAsync(request.Id, ct);
        if (setting == null)
        {
            _logger.LogWarning("Platform setting {SettingId} not found", request.Id);
            return _messages.Fail<PlatformSettingDto>(
                ApplicationErrors.PlatformSetting.NOT_FOUND,
                MessageType.NotFound);
        }

        return _messages.Success(MapToDto(setting), "PlatformSetting.Detail");
    }

    private static PlatformSettingDto MapToDto(PlatformSetting setting) => new(
        setting.Id,
        setting.Key,
        setting.IsEncrypted ? "***" : setting.Value,
        setting.Description,
        setting.Category,
        setting.ValueType,
        setting.IsEncrypted,
        setting.IsPublic,
        setting.CreatedAt
    );
}
