using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Commands.UpdateBranding;

public record UpdateBrandingCommand(string LogoUrl, string PrimaryColor, string AccentColor) : ICommand<Response<BrandingDto>>;
