namespace CustomerSupport.Application.Features.PlatformSettings.Dtos;

public record BrandingDto(
    string LogoUrl,
    string PrimaryColor,
    string AccentColor
);
