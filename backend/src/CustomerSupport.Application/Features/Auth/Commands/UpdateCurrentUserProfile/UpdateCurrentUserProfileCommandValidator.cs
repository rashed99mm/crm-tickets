using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Auth.Dtos;
using FluentValidation;

namespace CustomerSupport.Application.Features.Auth.Commands.UpdateCurrentUserProfile;

/// <summary>
/// Field-level rules for the self-service profile update (AC-433, AC-434, AC-435). The pipeline
/// validation behavior runs this before the handler and, on failure, returns a field-keyed 400 —
/// the same shape the frontend's error binding expects.
/// </summary>
public class UpdateCurrentUserProfileCommandValidator : AbstractValidator<UpdateCurrentUserProfileCommand>
{
    private const int NameMaxLength = 100;
    private const int ImageUrlMaxLength = 8_000_000;

    public UpdateCurrentUserProfileCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required").WithErrorCode(ApplicationErrors.Validation.FIRST_NAME_REQUIRED)
            .MaximumLength(NameMaxLength).WithMessage($"First name must not exceed {NameMaxLength} characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required").WithErrorCode(ApplicationErrors.Validation.LAST_NAME_REQUIRED)
            .MaximumLength(NameMaxLength).WithMessage($"Last name must not exceed {NameMaxLength} characters").WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH);

        RuleFor(x => x.PhoneNumber)
            .Must(BeNormalizedPhone).When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone number must be in E.164 format, e.g. +14155550100")
            .WithErrorCode(ApplicationErrors.Validation.INVALID_PHONE);

        RuleFor(x => x.ProfileImageUrl)
            .MaximumLength(ImageUrlMaxLength)
            .WithMessage($"Profile image must not exceed {ImageUrlMaxLength} characters")
            .WithErrorCode(ApplicationErrors.Validation.MAX_LENGTH)
            .Must(BeValidImageReference).When(x => !string.IsNullOrWhiteSpace(x.ProfileImageUrl))
            .WithMessage("Profile image must be an absolute https URL or a valid image data URL")
            .WithErrorCode(ApplicationErrors.Validation.INVALID_FORMAT);
    }

    private static bool BeNormalizedPhone(string? phone) =>
        phone is not null && System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+[1-9]\d{7,14}$");

    private static bool BeAbsoluteHttpsUrl(string? url) =>
        url is not null &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;

    private static bool BeValidImageReference(string? value) =>
        BeAbsoluteHttpsUrl(value) || IsBase64ImageDataUrl(value);

    private static bool IsBase64ImageDataUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separator = value.IndexOf(",", StringComparison.Ordinal);
        if (separator < 0 || !value[..separator].Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            Convert.FromBase64String(value[(separator + 1)..]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
