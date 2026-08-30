using CustomerSupport.Application.Features.Notifications.Commands.CreateNotification;
using CustomerSupport.Application.Features.Notifications.Validators;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Validators;

public class CreateNotificationRequestValidatorTests
{
    private readonly CreateNotificationRequestValidator _validator = new();

    #region UserId Validation

    [Fact]
    public async Task Validate_ValidUserId_ShouldPass()
    {
        var request = CreateValidRequest(userId: Guid.NewGuid());
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_EmptyUserId_ShouldFail()
    {
        var request = CreateValidRequest(userId: Guid.Empty);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId" && e.ErrorMessage.Contains("required"));
    }

    #endregion

    #region Title Validation

    [Fact]
    public async Task Validate_ValidTitle_ShouldPass()
    {
        var request = CreateValidRequest(title: "Notification Title");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyTitle_ShouldFail()
    {
        var request = CreateValidRequest(title: "");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public async Task Validate_TitleExceeds200Chars_ShouldFail()
    {
        var longTitle = new string('a', 201);
        var request = CreateValidRequest(title: longTitle);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage.Contains("200"));
    }

    #endregion

    #region Message Validation

    [Fact]
    public async Task Validate_ValidMessage_ShouldPass()
    {
        var request = CreateValidRequest(message: "This is a notification message.");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyMessage_ShouldFail()
    {
        var request = CreateValidRequest(message: "");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Message" && e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public async Task Validate_MessageExceeds2000Chars_ShouldFail()
    {
        var longMessage = new string('a', 2001);
        var request = CreateValidRequest(message: longMessage);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Message" && e.ErrorMessage.Contains("2000"));
    }

    #endregion

    #region NotificationType Validation

    [Theory]
    [InlineData("Info")]
    [InlineData("Warning")]
    [InlineData("Success")]
    [InlineData("Error")]
    public async Task Validate_ValidNotificationType_ShouldPass(string notificationType)
    {
        var request = CreateValidRequest(notificationType: notificationType);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyNotificationType_ShouldFail()
    {
        var request = CreateValidRequest(notificationType: "");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NotificationType" && e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public async Task Validate_NotificationTypeExceeds50Chars_ShouldFail()
    {
        var longType = new string('a', 51);
        var request = CreateValidRequest(notificationType: longType);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NotificationType" && e.ErrorMessage.Contains("50"));
    }

    #endregion

    #region Channel Validation

    [Theory]
    [InlineData("InApp")]
    [InlineData("Email")]
    [InlineData("SMS")]
    [InlineData("Push")]
    public async Task Validate_ValidChannel_ShouldPass(string channel)
    {
        var request = CreateValidRequest(channel: channel);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyChannel_ShouldFail()
    {
        var request = CreateValidRequest(channel: "");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Channel" && e.ErrorMessage.Contains("required"));
    }

    [Theory]
    [InlineData("Web")]
    [InlineData("API")]
    [InlineData("Fax")]
    public async Task Validate_InvalidChannel_ShouldFail(string channel)
    {
        var request = CreateValidRequest(channel: channel);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Channel" && e.ErrorMessage.Contains("InApp, Email, SMS, or Push"));
    }

    #endregion

    #region Metadata Validation

    [Fact]
    public async Task Validate_NullMetadata_ShouldPass()
    {
        var request = CreateValidRequest(metadata: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue(); // Metadata is optional
    }

    [Fact]
    public async Task Validate_ValidMetadata_ShouldPass()
    {
        var request = CreateValidRequest(metadata: "{\"key\":\"value\"}");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static CreateNotificationRequest CreateValidRequest(
        Guid? userId = null,
        string? title = null,
        string? message = null,
        string? notificationType = null,
        string? channel = null,
        string? metadata = null)
    {
        return new CreateNotificationRequest(
            UserId: userId ?? Guid.NewGuid(),
            Title: title ?? "Test Notification",
            Message: message ?? "Test notification message",
            NotificationType: notificationType ?? "Info",
            Channel: channel ?? "InApp",
            Metadata: metadata
        );
    }

    #endregion
}
