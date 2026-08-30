using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;

using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Commands.DeletePlatformSetting;

public record DeletePlatformSettingCommand(Guid Id) : ICommand<Response<Unit>>;
