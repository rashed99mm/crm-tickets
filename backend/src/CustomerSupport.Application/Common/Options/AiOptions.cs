namespace CustomerSupport.Application.Common.Options;

/// <summary>
/// The AI provider binding. AI-30 — one or more named providers are configured; <see cref="Active"/>
/// names the one that serves first and <see cref="Fallbacks"/> the ordered remainder. A provider
/// entry speaks either the OpenAI-schema <c>/chat/completions</c> protocol (OpenAI, Azure,
/// OpenRouter, Groq, Mistral, Ollama), Anthropic's Messages API, or Google's Gemini API — the
/// adapter is chosen by name, so a model change is configuration, not a redeploy.
///
/// Keys arrive from user-secrets or environment and must never appear in a response or log (A3).
/// The legacy flat <c>Ai:BaseUrl/ApiKey/Model</c> keys still bind through
/// <see cref="ProviderOptions.ConfigureLegacyFallback"/> so existing deployments keep working.
/// </summary>
public class AiOptions
{
    public const string SectionName = "Ai";

    public const int DefaultTimeoutSeconds = 20;
    public const int DefaultMaxOutputTokens = 1024;

    /// <summary>Follows the platform's `__SET_` placeholder convention from appsettings.</summary>
    private const string PlaceholderMarker = "__SET_";

    public string Active { get; set; } = "openai-compatible";

    public List<ProviderOptions> Providers { get; set; } = [];

    /// <summary>Ordered provider names tried after <see cref="Active"/> fails (AI-31).</summary>
    public List<string> Fallbacks { get; set; } = [];

    /// <summary>Optional regex patterns scrubbed from every prompt before dispatch (AI-37).</summary>
    public List<string> PiiScrub { get; set; } = [];

    /// <summary>Circuit breaker: consecutive failures before a provider is skipped (AI-33).</summary>
    public int BreakerFailureThreshold { get; set; } = 3;

    /// <summary>Circuit breaker cooldown before a skipped provider is retried (AI-33).</summary>
    public int BreakerCooldownSeconds { get; set; } = 60;

    /// <summary>
    /// Back-compat: a deployment that still configures the flat legacy keys gets an implicit
    /// provider so nothing breaks on upgrade (A2 keeps an empty key degrading to NoOp).
    /// </summary>
    public void ConfigureLegacyFallback(string? baseUrl, string? apiKey, string? model, int timeoutSeconds)
    {
        if (Providers.Count > 0 || string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        Providers.Add(new ProviderOptions
        {
            Name = Active,
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://openrouter.ai/api/v1" : baseUrl,
            ApiKey = apiKey,
            Model = string.IsNullOrWhiteSpace(model) ? "meta-llama/llama-3.3-70b-instruct:free" : model,
            TimeoutSeconds = timeoutSeconds <= 0 ? DefaultTimeoutSeconds : timeoutSeconds,
        });
    }

    public ProviderOptions? GetProvider(string name) =>
        Providers.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public ProviderOptions? GetActiveProvider() => GetProvider(Active);

    /// <summary>True when the active provider has credentials — the NoOp/AI registration switch (A2).</summary>
    public bool IsAvailable => GetActiveProvider()?.IsConfigured == true;
}

/// <summary>One named provider endpoint. <see cref="Protocol"/> selects the adapter.</summary>
public class ProviderOptions
{
    public const string ProtocolOpenAi = "openai-compatible";
    public const string ProtocolAnthropic = "anthropic";
    public const string ProtocolGemini = "gemini";

    public string Name { get; set; } = "openai-compatible";

    /// <summary>Wire protocol; defaults to the OpenAI schema the platform has always spoken.</summary>
    public string Protocol { get; set; } = ProtocolOpenAi;

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    /// <summary>Empty in evaluation deployments — the whole AI surface degrades to NoOp (A2).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "meta-llama/llama-3.3-70b-instruct:free";

    public int TimeoutSeconds { get; set; } = AiOptions.DefaultTimeoutSeconds;

    public int MaxOutputTokens { get; set; } = AiOptions.DefaultMaxOutputTokens;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !ApiKey.Contains("__SET_", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(BaseUrl);
}
