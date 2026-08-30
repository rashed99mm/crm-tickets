namespace CustomerSupport.Application.Ai;

/// <summary>One message handed to a provider. Roles mirror the chat-completion vocabulary.</summary>
public sealed record AiPromptMessage(string Role, string Content);

/// <summary>What a feature asks a provider for. Provider-agnostic by design.</summary>
public sealed record AiChatRequest(
    IReadOnlyList<AiPromptMessage> Messages,
    double Temperature,
    int MaxOutputTokens);

/// <summary>
/// The provider's answer plus the usage an operator needs. Tokens are reported per call so cost
/// and budget stay observable (AI-34); a provider that does not expose usage reports zero.
/// </summary>
public sealed record AiChatResult(string Text, int PromptTokens, int CompletionTokens);

/// <summary>
/// AI-30 — the raw provider port. One implementation per wire protocol
/// (OpenAI-compatible, Anthropic, Gemini); the resilient selection over them lives in the
/// provider factory, and the feature-facing <see cref="IAiService"/> prompts sit above both.
/// </summary>
public interface IAiProvider
{
    /// <summary>The registered provider name this adapter serves (config lookup key).</summary>
    string Name { get; }

    /// <summary>False when this provider's credentials are absent or placeholders.</summary>
    bool IsConfigured { get; }

    Task<AiOutcome<AiChatResult>> CompleteAsync(AiChatRequest request, CancellationToken ct);
}

