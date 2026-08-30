using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Ai;

/// <summary>An article the suggestion or answer drew on. Published articles only (AC-21.7, A4).</summary>
public sealed record KbCitation(Guid ArticleId, string Title);

/// <summary>Retrieved article text handed to the model as grounding context.</summary>
public sealed record KbPassage(Guid ArticleId, string Title, string Body);

/// <summary>The chatbot's grounded answer plus what grounded it (AC-21.9).</summary>
public sealed record AiAnswer(string Text, IReadOnlyList<KbCitation> Citations);

/// <summary>A category option for US-705 — always real seeded categories, never free text.</summary>
public sealed record CategorySuggestion(Guid Id, string Name);

/// <summary>
/// The outcome of one provider call: success with a value, or a reason that maps to a documented
/// system code at the feature boundary. Deliberately not <see cref="Response{T}"/> — this is the
/// provider port's own vocabulary; enveloping happens in features.
/// </summary>
public sealed record AiOutcome<T>(bool Success, T? Value, string? Error)
{
    public static AiOutcome<T> Ok(T value) => new(true, value, null);
    public static AiOutcome<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// FEAT-21 / AC-21.2 — the single AI port. Application knows nothing about HTTP gateways; the
/// Infrastructure implementation speaks OpenAI-schema JSON over an OpenRouter-compatible endpoint
/// (A1). When no key is configured a NoOp implementation registers instead and reports
/// <see cref="IsAvailable"/> false (A2), which features translate into the documented
/// ERR052 "not configured" envelope rather than an exception.
/// </summary>
public interface IAiService
{
    /// <summary>False when running degraded (no credentials). Features consult it before calling.</summary>
    bool IsAvailable { get; }

    /// <summary>US-704 — a short summary of a ticket thread.</summary>
    Task<AiOutcome<string>> SummariseAsync(string threadText, CancellationToken ct);

    /// <summary>
    /// AC-21.11 / A5 — a single-word sentiment label. <see cref="AiOutcome{T}.Fail"/> is
    /// translated by the caller into <c>null</c>; the summary never fails on a sentiment error.
    /// </summary>
    Task<AiOutcome<string?>> ClassifySentimentAsync(string threadText, CancellationToken ct);

    /// <summary>US-706 — a customer-ready reply draft the agent will edit before sending.</summary>
    Task<AiOutcome<string>> DraftReplyAsync(string threadText, string? extraInstruction, CancellationToken ct);

    /// <summary>US-705 — categories chosen from the platform's real list only.</summary>
    Task<AiOutcome<IReadOnlyList<string>>> SuggestCategoriesAsync(
        string threadText, IReadOnlyList<string> categoryNames, CancellationToken ct);

    /// <summary>US-707 — published KB articles likely to contain the solution.</summary>
    Task<AiOutcome<IReadOnlyList<KbCitation>>> SuggestSolutionsAsync(
        string question, IReadOnlyList<KbPassage> candidates, CancellationToken ct);

    /// <summary>QA behaviour — answers strictly from <paramref name="passages"/> (A4).</summary>
    Task<AiOutcome<string>> AnswerAsync(string question, IReadOnlyList<KbPassage> passages, CancellationToken ct);
}

/// <summary>Shared prompt/protocol constants for the port's implementations.</summary>
public static class AiContract
{
    /// <summary>
    /// A4 — when retrieval found nothing relevant the model is instructed to answer with exactly
    /// this sentinel, and callers convert it into the QA001 ungrounded refusal.
    /// </summary>
    public const string UngroundedSentinel = "[NOT_IN_KB]";
}
