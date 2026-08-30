using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Auth.Commands.UpdateCurrentUserProfile;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Validators;

public class UpdateCurrentUserProfileCommandValidatorTests
{
    private readonly UpdateCurrentUserProfileCommandValidator _validator = new();

    [Fact]
    public async Task Validate_Base64ImageDataUrl_ShouldPass()
    {
        var result = await _validator.ValidateAsync(new UpdateCurrentUserProfileCommand(
            "Demo", "User", null, "data:image/png;base64,iVBORw0KGgo="));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_MalformedImageReference_ShouldFailWithLocalizedCode()
    {
        var result = await _validator.ValidateAsync(new UpdateCurrentUserProfileCommand(
            "Demo", "User", null, "data:image/png;base64,not-base64"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "ProfileImageUrl" &&
            e.ErrorCode == ApplicationErrors.Validation.INVALID_FORMAT);
    }

    [Fact]
    public async Task Validate_OptionalFieldsMissing_ShouldPass()
    {
        var result = await _validator.ValidateAsync(new UpdateCurrentUserProfileCommand(
            "Demo", "User", null, null));

        result.IsValid.Should().BeTrue();
    }
}
