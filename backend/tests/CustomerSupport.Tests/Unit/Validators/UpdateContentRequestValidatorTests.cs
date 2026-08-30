using CustomerSupport.Application.Features.Contents.Commands.UpdateContent;
using CustomerSupport.Application.Features.Contents.Validators;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Validators;

public class UpdateContentRequestValidatorTests
{
    private readonly UpdateContentRequestValidator _validator = new();

    #region Valid Request

    [Fact]
    public async Task Validate_AllNulls_ShouldPass()
    {
        var request = new UpdateContentRequest(
            Title: null, Body: null, Summary: null, Status: null,
            FeaturedImageUrl: null, Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidTitle_ShouldPass()
    {
        var request = new UpdateContentRequest(
            Title: "Updated Title", Body: null, Summary: null, Status: null,
            FeaturedImageUrl: null, Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Published")]
    [InlineData("Archived")]
    public async Task Validate_ValidStatus_ShouldPass(string status)
    {
        var request = new UpdateContentRequest(
            Title: null, Body: null, Summary: null, Status: status,
            FeaturedImageUrl: null, Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Title Validation

    [Fact]
    public async Task Validate_EmptyTitle_ShouldPass()
    {
        var request = new UpdateContentRequest(
            Title: "", Body: null, Summary: null, Status: null,
            FeaturedImageUrl: null, Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_TitleExceeds500Chars_ShouldFail()
    {
        var longTitle = new string('a', 501);
        var request = new UpdateContentRequest(
            Title: longTitle, Body: null, Summary: null, Status: null,
            FeaturedImageUrl: null, Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage.Contains("500"));
    }

    #endregion

    #region Summary Validation

    [Fact]
    public async Task Validate_EmptySummary_ShouldPass()
    {
        var request = new UpdateContentRequest(
            Title: null, Body: null, Summary: "", Status: null,
            FeaturedImageUrl: null, Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_SummaryExceeds1000Chars_ShouldFail()
    {
        var longSummary = new string('a', 1001);
        var request = new UpdateContentRequest(
            Title: null, Body: null, Summary: longSummary, Status: null,
            FeaturedImageUrl: null, Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Summary" && e.ErrorMessage.Contains("1000"));
    }

    #endregion

    #region Status Validation

    [Theory]
    [InlineData("Pending")]
    [InlineData("Active")]
    [InlineData("Deleted")]
    [InlineData("Review")]
    public async Task Validate_InvalidStatus_ShouldFail(string status)
    {
        var request = new UpdateContentRequest(
            Title: null, Body: null, Summary: null, Status: status,
            FeaturedImageUrl: null, Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Status" && e.ErrorMessage.Contains("Draft, Published, or Archived"));
    }

    #endregion

    #region FeaturedImageUrl Validation

    [Fact]
    public async Task Validate_EmptyFeaturedImageUrl_ShouldPass()
    {
        var request = new UpdateContentRequest(
            Title: null, Body: null, Summary: null, Status: null,
            FeaturedImageUrl: "", Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_FeaturedImageUrlExceeds2000Chars_ShouldFail()
    {
        var longUrl = "https://example.com/" + new string('a', 2000);
        var request = new UpdateContentRequest(
            Title: null, Body: null, Summary: null, Status: null,
            FeaturedImageUrl: longUrl, Tags: null, Category: null,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FeaturedImageUrl" && e.ErrorMessage.Contains("2000"));
    }

    #endregion

    #region Category Validation

    [Fact]
    public async Task Validate_EmptyCategory_ShouldPass()
    {
        var request = new UpdateContentRequest(
            Title: null, Body: null, Summary: null, Status: null,
            FeaturedImageUrl: null, Tags: null, Category: "",
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_CategoryExceeds100Chars_ShouldFail()
    {
        var longCategory = new string('a', 101);
        var request = new UpdateContentRequest(
            Title: null, Body: null, Summary: null, Status: null,
            FeaturedImageUrl: null, Tags: null, Category: longCategory,
            PublishedAt: null, ExpiresAt: null, IsFeatured: null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Category" && e.ErrorMessage.Contains("100"));
    }

    #endregion
}
