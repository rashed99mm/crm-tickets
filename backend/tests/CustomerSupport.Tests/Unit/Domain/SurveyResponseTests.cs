using CustomerSupport.Domain.Entities.Survey;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>
/// <see cref="SurveyResponse"/> invariants (US-408, PJ-11): a 1–5 rating with optional bounded free
/// text, recorded once per ticket. The unique TicketId index and the append-only guard are enforced
/// in persistence — the submit command asserts the "once" rule against the store, not here.
/// </summary>
public class SurveyResponseTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [Trait("AC", "408")]
    public void PJ11_Valid_Ratings_Are_Accepted(int rating)
    {
        var response = SurveyResponse.Create(Guid.NewGuid(), rating, null);

        response.Rating.Should().Be(rating);
        response.TicketId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    [Trait("AC", "408")]
    public void PJ11_Rating_Below_Minimum_Throws()
    {
        var act = () => SurveyResponse.Create(Guid.NewGuid(), SurveyResponse.MinRating - 1, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("AC", "408")]
    public void PJ11_Rating_Above_Maximum_Throws()
    {
        var act = () => SurveyResponse.Create(Guid.NewGuid(), SurveyResponse.MaxRating + 1, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("AC", "408")]
    public void PJ11_Free_Text_Is_Optional(string? freeText)
    {
        var response = SurveyResponse.Create(Guid.NewGuid(), 4, freeText);

        response.FreeText.Should().BeNull();
    }

    [Fact]
    public void PJ11_Free_Text_Is_Trimmed()
    {
        SurveyResponse.Create(Guid.NewGuid(), 4, "  good service  ").FreeText.Should().Be("good service");
    }

    [Fact]
    public void PJ11_Free_Text_Too_Long_Throws()
    {
        var longText = new string('x', SurveyResponse.MaxFreeTextLength + 1);

        var act = () => SurveyResponse.Create(Guid.NewGuid(), 4, longText);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PJ11_At_Boundary_Free_Text_Is_Accepted()
    {
        var boundary = new string('x', SurveyResponse.MaxFreeTextLength);

        SurveyResponse.Create(Guid.NewGuid(), 4, boundary).FreeText.Should().HaveLength(SurveyResponse.MaxFreeTextLength);
    }

    [Fact]
    public void PJ11_Empty_TicketId_Throws()
    {
        var act = () => SurveyResponse.Create(Guid.Empty, 4, null);

        act.Should().Throw<ArgumentException>();
    }
}