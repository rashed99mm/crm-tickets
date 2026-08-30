using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.PlatformSettings;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using System.Text.RegularExpressions;

namespace CustomerSupport.Application.Features.PlatformSettings.Commands.UpdateBranding;

public class UpdateBrandingCommandHandler(
    IRepository<PlatformSetting> repo,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<UpdateBrandingCommand, Response<BrandingDto>>
{
    private static readonly Regex ColorHex = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public async Task<Response<BrandingDto>> Handle(UpdateBrandingCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.LogoUrl) && !Uri.TryCreate(request.LogoUrl, UriKind.Absolute, out _))
            return messages.Fail<BrandingDto>(ApplicationErrors.Validation.INVALID_FORMAT, MessageType.Validation);

        if (!ColorHex.IsMatch(request.PrimaryColor))
            return messages.Fail<BrandingDto>(ApplicationErrors.Validation.INVALID_FORMAT, MessageType.Validation);

        if (!ColorHex.IsMatch(request.AccentColor))
            return messages.Fail<BrandingDto>(ApplicationErrors.Validation.INVALID_FORMAT, MessageType.Validation);

        await UpsertSettingAsync(BrandingKeys.LogoUrl, request.LogoUrl ?? "", "Brand logo URL", ct);
        await UpsertSettingAsync(BrandingKeys.PrimaryColor, request.PrimaryColor, "Brand primary colour", ct);
        await UpsertSettingAsync(BrandingKeys.AccentColor, request.AccentColor, "Brand accent colour", ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(
            new BrandingDto(request.LogoUrl ?? "", request.PrimaryColor, request.AccentColor),
            "Branding.Update");
    }

    private async Task UpsertSettingAsync(string key, string value, string description, CancellationToken ct)
    {
        var existing = await repo.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is null)
        {
            await repo.AddAsync(new PlatformSetting
            {
                Key = key,
                Value = value,
                Description = description,
                Category = "Branding",
                IsPublic = true
            }, ct);
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
            repo.Update(existing);
        }
    }
}
