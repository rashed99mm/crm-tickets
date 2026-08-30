using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Commands.UpdatePlatformSetting;

public record UpdatePlatformSettingCommand(
    Guid Id,
    string? Value,
    string? Description,
    bool? IsEncrypted,
    bool? IsPublic
) : ICommand<Response<Guid>>;
