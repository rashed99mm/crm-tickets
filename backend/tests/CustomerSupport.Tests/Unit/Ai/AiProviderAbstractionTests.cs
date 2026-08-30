using CustomerSupport.Application.Ai;
using CustomerSupport.Domain.Entities.Ai;
using CustomerSupport.Infrastructure.Ai;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Ai;

/// <summary>AI-35 — bilingual BM25-style retrieval quality is the feature's ground truth.</summary>
public class KbRetrieverTests
{
    private static readonly KbPassage[] Corpus =
    [
        new(Guid.NewGuid(), "How to reset your password",
            "Use the forgot password link on the sign-in page. A reset email arrives within a minute."),
        new(Guid.NewGuid(), "تحديث بيانات الفاتورة",
            "يمكن تحديث بيانات الفاتورة من صفحة الحساب، ثم اختيار الفاتورة المطلوبة وتعديلها."),
        new(Guid.NewGuid(), "Billing cycles explained",
            "Your billing cycle renews on the first day of each month. Invoices are issued automatically."),
    ];

    [Fact]
    public void EnglishQuestion_RetrievesMatchingArticle()
    {
        var results = KbRetriever.Retrieve("I forgot my password and cannot sign in", Corpus, 2);

        results.Should().NotBeEmpty();
        results[0].Title.Should().Be("How to reset your password");
    }

    [Fact]
    public void ArabicQuestion_RetrievesMatchingArticle_AfterNormalization()
    {
        // Diacritics + alef/ta-marbuta variants must not defeat the match (AI-35).
        var results = KbRetriever.Retrieve("كيف أُحدّث بيانات الفاتورة؟", Corpus, 2);

        results.Should().NotBeEmpty();
        results[0].Title.Should().Be("تحديث بيانات الفاتورة");
    }

    [Fact]
    public void TitleMatch_OutranksBodyMatch()
    {
        var results = KbRetriever.Retrieve("billing", Corpus, 3);

        results[0].Title.Should().Be("Billing cycles explained");
    }

    [Fact]
    public void EmptyCorpus_ReturnsNothing_SoCallerRefuses()
    {
        KbRetriever.Retrieve("anything at all", [], 5).Should().BeEmpty();
    }

    [Fact]
    public void IrrelevantQuestion_ScoresNothing()
    {
        KbRetriever.Retrieve("quantum flux capacitor calibration", Corpus, 3).Should().BeEmpty();
    }
}

/// <summary>AI-36 — schema-told answers parse strictly or fail safely.</summary>
public class AiJsonTests
{
    [Fact]
    public void ValidItemsObject_ParsesStrings()
    {
        var parsed = AiJson.ParseStringArray("""{"items":["Billing","Accounts"]}""");
        parsed.Should().BeEquivalentTo(["Billing", "Accounts"]);
    }

    [Fact]
    public void NonJson_ReturnsNull()
    {
        AiJson.ParseStringArray("Billing, Accounts").Should().BeNull();
    }

    [Fact]
    public void MissingItems_ReturnsNull()
    {
        AiJson.ParseStringArray("""{"choices":[]}""").Should().BeNull();
    }

    [Fact]
    public void NonStringEntries_AreDropped()
    {
        var parsed = AiJson.ParseStringArray("""{"items":["Billing", 5, null, "Accounts"]}""");
        parsed.Should().BeEquivalentTo(["Billing", "Accounts"]);
    }

    [Theory]
    [Trait("AC", "21.11")]
    [InlineData("""{"items":["Frustrated"]}""", "Frustrated")]
    [InlineData("""{"items":["Neutral"]}""", "Neutral")]
    [InlineData("""{"items":["Satisfied"]}""", "Satisfied")]
    public void ValidSentiment_ParsesLabel(string raw, string expected)
    {
        AiJson.ParseSentiment(raw).Should().Be(expected);
    }

    [Theory]
    [Trait("AC", "21.11")]
    [InlineData("""{"items":["Ecstatic"]}""")]
    [InlineData("""{"items":["frustrated"]}""")]
    [InlineData("""{"choices":["Frustrated"]}""")]
    [InlineData("""{"items":[]}""")]
    public void UnknownOrUnparseableSentiment_ReturnsNull(string raw)
    {
        AiJson.ParseSentiment(raw).Should().BeNull();
    }

    [Fact]
    [Trait("AC", "21.11")]
    public void GarbageSentiment_ReturnsNull()
    {
        AiJson.ParseSentiment("not json at all").Should().BeNull();
    }

    [Theory]
    [Trait("AC", "21.11")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrNullSentiment_ReturnsNull(string? raw)
    {
        AiJson.ParseSentiment(raw).Should().BeNull();
    }
}

/// <summary>AI-40/AI-42 — session ownership, scoping and lifecycle invariants.</summary>
public class AiChatSessionDomainTests
{
    [Fact]
    public void BelongsTo_MatchesActorAndScopeOnly()
    {
        var actor = Guid.NewGuid();
        var session = AiChatSession.Create(actor, AiChatScope.Staff);

        session.BelongsTo(actor, AiChatScope.Staff).Should().BeTrue();
        session.BelongsTo(actor, AiChatScope.Portal).Should().BeFalse();
        session.BelongsTo(Guid.NewGuid(), AiChatScope.Staff).Should().BeFalse();
    }

    [Fact]
    public void AttachTicket_ClosesTheSession()
    {
        var session = AiChatSession.Create(Guid.NewGuid(), AiChatScope.Portal);

        session.AttachTicket(Guid.NewGuid());

        session.Status.Should().Be(AiChatStatus.Closed);
        session.TicketId.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithoutActor_Throws()
    {
        var act = () => AiChatSession.Create(Guid.Empty, AiChatScope.Staff);
        act.Should().Throw<ArgumentException>();
    }
}

