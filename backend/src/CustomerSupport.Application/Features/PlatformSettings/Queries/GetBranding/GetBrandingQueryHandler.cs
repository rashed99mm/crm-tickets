using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Queries.GetBranding;

public class GetBrandingQueryHandler(
    IRepository<Domain.Entities.PlatformSettings.PlatformSetting> repo,
    IMessageFactory messages)
    : IQueryHandler<GetBrandingQuery, Response<BrandingDto>>
{
    public async Task<Response<BrandingDto>> Handle(GetBrandingQuery request, CancellationToken ct)
    {
        var settings = await repo.ListAsync(null, ct);

        var logo = settings.FirstOrDefault(s => s.Key == BrandingKeys.LogoUrl);
        var primary = settings.FirstOrDefault(s => s.Key == BrandingKeys.PrimaryColor);
        var accent = settings.FirstOrDefault(s => s.Key == BrandingKeys.AccentColor);

        // Unconfigured branding is not a missing resource. Nothing seeds the three brand.* settings,
        // and this endpoint is public and called by both shells on load — so failing here 404'd on
        // every page load of a fresh install, and painted "Setting not found" across the settings
        // screen's Global Branding panel. The defaults below are the answer in that case; the old
        // guard computed them and then threw them away.
        return messages.Success(
            new BrandingDto(
                LogoUrl: logo?.Value ?? "",
                PrimaryColor: primary?.Value ?? "#2563EB",
                AccentColor: accent?.Value ?? "#2563EB"),
            "Branding.View");
    }
}
