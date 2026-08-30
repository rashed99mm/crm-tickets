using System.Text.Json;
using CustomerSupport.Domain.Entities.Ai;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>
/// AC-21.11 — the <see cref="AiSuggestion.AiSentiment"/> enum round-trips through the JSON shape
/// the handler writes, with <c>null</c> for "no sentiment". The string-name serialisation is what
/// makes the wire format independent of enum-member order in source.
/// </summary>
public class AiSuggestionEntityTests
{
    [Fact]
    [Trait("AC", "21.11")]
    public void AiSentiment_SerialisesAsStringName()
    {
        var payload = JsonSerializer.Serialize(new
        {
            text = "Customer cannot sign in.",
            sentiment = AiSuggestion.AiSentiment.Frustrated.ToString(),
        });

        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("sentiment").GetString().Should().Be("Frustrated");
    }

    [Fact]
    [Trait("AC", "21.11")]
    public void AiSentiment_Null_RoundtripsAsJsonNull()
    {
        string? sentiment = null;
        var payload = JsonSerializer.Serialize(new { text = "x", sentiment });

        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("sentiment").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [Trait("AC", "21.11")]
    [InlineData(AiSuggestion.AiSentiment.Frustrated, "Frustrated")]
    [InlineData(AiSuggestion.AiSentiment.Neutral, "Neutral")]
    [InlineData(AiSuggestion.AiSentiment.Satisfied, "Satisfied")]
    public void AiSentiment_AllValues_SerialiseAsTheirName(
        AiSuggestion.AiSentiment value, string expected)
    {
        AiSuggestion.AiSentiment
            .Parse(typeof(AiSuggestion.AiSentiment), expected)
            .Should()
            .Be(value);
    }
}
