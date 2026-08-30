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

        if (logo is null && primary is null && accent is null)
        {
            return messages.Fail<BrandingDto>(
                ApplicationErrors.PlatformSetting.NOT_FOUND,
                MessageType.NotFound);
        }

        return messages.Success(
            new BrandingDto(
                LogoUrl: logo?.Value ?? "",
                PrimaryColor: primary?.Value ?? "#2563EB",
                AccentColor: accent?.Value ?? "#2563EB"),
            "Branding.View");
    }
}
