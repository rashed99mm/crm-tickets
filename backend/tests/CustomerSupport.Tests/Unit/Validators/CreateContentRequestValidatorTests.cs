using CustomerSupport.Application.Features.Contents.Commands.CreateContent;
using CustomerSupport.Application.Features.Contents.Validators;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Validators;

public class CreateContentRequestValidatorTests
{
    private readonly CreateContentRequestValidator _validator = new();

    #region Title Validation

    [Fact]
    public async Task Validate_ValidTitle_ShouldPass()
    {
        var request = CreateValidRequest(title: "Test Content Title");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
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
    public async Task Validate_TitleExceeds500Chars_ShouldFail()
    {
        var longTitle = new string('a', 501);
        var request = CreateValidRequest(title: longTitle);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage.Contains("500"));
    }

    #endregion

    #region Body Validation

    [Fact]
    public async Task Validate_EmptyBody_ShouldFail()
    {
        var request = CreateValidRequest(body: "");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Body" && e.ErrorMessage.Contains("required"));
    }

    #endregion

    #region Summary Validation

    [Fact]
    public async Task Validate_ValidSummary_ShouldPass()
    {
        var request = CreateValidRequest(summary: "This is a valid summary.");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_NullSummary_ShouldPass()
    {
        var request = CreateValidRequest(summary: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue(); // Summary is optional
    }

    [Fact]
    public async Task Validate_SummaryExceeds1000Chars_ShouldFail()
    {
        var longSummary = new string('a', 1001);
        var request = CreateValidRequest(summary: longSummary);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Summary" && e.ErrorMessage.Contains("1000"));
    }

    #endregion

    #region ContentType Validation

    [Theory]
    [InlineData("Article")]
    [InlineData("News")]
    [InlineData("Event")]
    public async Task Validate_ValidContentType_ShouldPass(string contentType)
    {
        var request = CreateValidRequest(contentType: contentType);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyContentType_ShouldFail()
    {
        var request = CreateValidRequest(contentType: "");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContentType" && e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public async Task Validate_ContentTypeExceeds50Chars_ShouldFail()
    {
        var longContentType = new string('a', 51);
        var request = CreateValidRequest(contentType: longContentType);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContentType" && e.ErrorMessage.Contains("50"));
    }

    #endregion

    #region Status Validation

    [Theory]
    [InlineData("Draft")]
    [InlineData("Published")]
    [InlineData("Archived")]
    public async Task Validate_ValidStatus_ShouldPass(string status)
    {
        var request = CreateValidRequest(status: status);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyStatus_ShouldFail()
    {
        var request = CreateValidRequest(status: "");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Status" && e.ErrorMessage.Contains("required"));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Active")]
    [InlineData("Deleted")]
    public async Task Validate_InvalidStatus_ShouldFail(string status)
    {
        var request = CreateValidRequest(status: status);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Status" && e.ErrorMessage.Contains("Draft, Published, or Archived"));
    }

    #endregion

    #region AuthorId Validation

    [Fact]
    public async Task Validate_EmptyAuthorId_ShouldFail()
    {
        var request = CreateValidRequest(authorId: Guid.Empty);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AuthorId" && e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public async Task Validate_ValidAuthorId_ShouldPass()
    {
        var request = CreateValidRequest(authorId: Guid.NewGuid());
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region FeaturedImageUrl Validation

    [Fact]
    public async Task Validate_NullFeaturedImageUrl_ShouldPass()
    {
        var request = CreateValidRequest(featuredImageUrl: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidFeaturedImageUrl_ShouldPass()
    {
        var request = CreateValidRequest(featuredImageUrl: "https://example.com/image.jpg");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_FeaturedImageUrlExceeds2000Chars_ShouldFail()
    {
        var longUrl = "https://example.com/" + new string('a', 2000);
        var request = CreateValidRequest(featuredImageUrl: longUrl);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FeaturedImageUrl" && e.ErrorMessage.Contains("2000"));
    }

    #endregion

    #region Category Validation

    [Fact]
    public async Task Validate_NullCategory_ShouldPass()
    {
        var request = CreateValidRequest(category: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidCategory_ShouldPass()
    {
        var request = CreateValidRequest(category: "Climate");
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_CategoryExceeds100Chars_ShouldFail()
    {
        var longCategory = new string('a', 101);
        var request = CreateValidRequest(category: longCategory);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Category" && e.ErrorMessage.Contains("100"));
    }

    #endregion

    #region Helper Methods

    private static CreateContentRequest CreateValidRequest(
        string? title = null,
        string? body = null,
        string? summary = null,
        string? contentType = null,
        Guid? authorId = null,
        string? status = null,
        string? featuredImageUrl = null,
        string[]? tags = null,
        string? category = null,
        DateTime? expiresAt = null,
        bool isFeatured = false)
    {
        return new CreateContentRequest(
            Title: title ?? "Test Title",
            Body: body ?? "Test body content",
            Summary: summary,
            ContentType: contentType ?? "Article",
            AuthorId: authorId ?? Guid.NewGuid(),
            Status: status ?? "Draft",
            FeaturedImageUrl: featuredImageUrl,
            Tags: tags ?? Array.Empty<string>(),
            Category: category,
            ExpiresAt: expiresAt,
            IsFeatured: isFeatured
        );
    }

    #endregion
}
