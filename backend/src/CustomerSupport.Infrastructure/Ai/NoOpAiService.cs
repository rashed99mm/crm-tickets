using CustomerSupport.Application.Ai;

namespace CustomerSupport.Infrastructure.Ai;

/// <summary>
/// FEAT-21 / A2 — the degraded mode. Registered when no provider key is configured, so an
/// evaluation deployment boots and every other capability keeps working; AI affordances answer
/// the documented "not configured" code at the feature boundary instead.
/// </summary>
public class NoOpAiService : IAiService
{
    public bool IsAvailable => false;

    public Task<AiOutcome<string>> SummariseAsync(string threadText, CancellationToken ct) =>
        Task.FromResult(AiOutcome<string>.Fail("AI assist is not configured"));

    public Task<AiOutcome<string?>> ClassifySentimentAsync(string threadText, CancellationToken ct) =>
        Task.FromResult(AiOutcome<string?>.Fail("AI assist is not configured"));

    public Task<AiOutcome<string>> DraftReplyAsync(string threadText, string? extraInstruction, CancellationToken ct) =>
        Task.FromResult(AiOutcome<string>.Fail("AI assist is not configured"));

    public Task<AiOutcome<IReadOnlyList<string>>> SuggestCategoriesAsync(
        string threadText, IReadOnlyList<string> categoryNames, CancellationToken ct) =>
        Task.FromResult(AiOutcome<IReadOnlyList<string>>.Fail("AI assist is not configured"));

    public Task<AiOutcome<IReadOnlyList<KbCitation>>> SuggestSolutionsAsync(
        string question, IReadOnlyList<KbPassage> candidates, CancellationToken ct) =>
        Task.FromResult(AiOutcome<IReadOnlyList<KbCitation>>.Fail("AI assist is not configured"));

    public Task<AiOutcome<string>> AnswerAsync(string question, IReadOnlyList<KbPassage> passages, CancellationToken ct) =>
        Task.FromResult(AiOutcome<string>.Fail("AI assist is not configured"));
}
