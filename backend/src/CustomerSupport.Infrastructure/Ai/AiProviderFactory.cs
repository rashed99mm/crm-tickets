using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Common.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Ai;

/// <summary>
/// AI-30/31/33 — resolves the named provider adapters and runs one resilient round trip over
/// them: retry with backoff on 429/5xx/timeout, a per-provider circuit breaker, then the ordered
/// fallback chain. The failure reason of the last attempt is preserved so features can answer
/// <c>AI_PROVIDER_FAILED</c> or <c>AI_RATE_LIMITED</c> instead of a generic internal error (AI-32).
/// Usage and latency are logged per call — never a key, prompt or response body (AI-34).
/// </summary>
public partial class AiProviderFactory
{
    private readonly AiOptions _options;
    private readonly IServiceProvider _services;
    private readonly ILogger<AiProviderFactory> _logger;
    private readonly ConcurrentDictionary<string, ProviderBreaker> _breakers = new();

    public AiProviderFactory(AiOptions options, IServiceProvider services, ILogger<AiProviderFactory> logger)
    {
        _options = options;
        _services = services;
        _logger = logger;
    }

    private sealed class ProviderBreaker
    {
        public int ConsecutiveFailures;
        public DateTime OpenUntilUtc = DateTime.MinValue;
    }

    public bool IsAvailable
    {
        get
        {
            var active = _options.GetActiveProvider();
            return active is not null && active.IsConfigured;
        }
    }

    /// <summary>The active provider's output cap, or a conservative default.</summary>
    public int MaxOutputTokens => _options.GetActiveProvider()?.MaxOutputTokens is int t and > 0
        ? t
        : AiOptions.DefaultMaxOutputTokens;

    /// <summary>
    /// One AI completion across the configured chain. Applies the PII scrub (AI-37) before any
    /// provider sees the prompt.
    /// </summary>
    public async Task<AiOutcome<AiChatResult>> CompleteAsync(AiChatRequest request, CancellationToken ct)
    {
        if (!IsAvailable)
        {
            return AiOutcome<AiChatResult>.Fail("AI assist is not configured");
        }

        request = Scrub(request);

        var chain = new[] { _options.Active }
            .Concat(_options.Fallbacks)
            .Select(_options.GetProvider)
            .Where(p => p is not null && p!.IsConfigured)
            .Select(p => p!)
            .ToList();

        if (chain.Count == 0)
        {
            return AiOutcome<AiChatResult>.Fail("AI assist is not configured");
        }

        string? lastError = null;

        foreach (var provider in chain)
        {
            var breaker = _breakers.GetOrAdd(provider.Name, _ => new ProviderBreaker());
            if (breaker.OpenUntilUtc > DateTime.UtcNow)
            {
                _logger.LogInformation("AI provider {Provider} skipped — breaker open", provider.Name);
                continue;
            }

            var outcome = await AttemptWithRetryAsync(provider, request, ct);

            if (outcome.Success)
            {
                breaker.ConsecutiveFailures = 0;
                return outcome;
            }

            lastError = outcome.Error;
            breaker.ConsecutiveFailures++;
            if (breaker.ConsecutiveFailures >= _options.BreakerFailureThreshold)
            {
                breaker.OpenUntilUtc = DateTime.UtcNow.AddSeconds(_options.BreakerCooldownSeconds);
                _logger.LogWarning("AI provider {Provider} breaker opened for {Seconds}s",
                    provider.Name, _options.BreakerCooldownSeconds);
            }
        }

        return AiOutcome<AiChatResult>.Fail(lastError ?? "AI assist failed");
    }

    /// <summary>The scrub a request went through, exposed for tests and observability.</summary>
    private AiChatRequest Scrub(AiChatRequest request)
    {
        if (_options.PiiScrub.Count == 0)
        {
            return request;
        }

        var patterns = _options.PiiScrub
            .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant))
            .ToList();

        return request with
        {
            Messages = request.Messages
                .Select(m => new AiPromptMessage(
                    m.Role,
                    patterns.Aggregate(m.Content, (acc, rx) => rx.Replace(acc, "[redacted]"))))
                .ToList(),
        };
    }

    private async Task<AiOutcome<AiChatResult>> AttemptWithRetryAsync(
        ProviderOptions provider, AiChatRequest request, CancellationToken ct)
    {
        var adapter = ResolveAdapter(provider);
        if (adapter is null)
        {
            return AiOutcome<AiChatResult>.Fail($"AI provider '{provider.Name}' has an unknown protocol");
        }

        var maxAttempts = 3;
        var started = DateTime.UtcNow;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var outcome = await adapter.CompleteAsync(request, ct);

            if (outcome.Success)
            {
                var result = outcome.Value!;
                _logger.LogInformation(
                    "AI call via {Provider} ({Model}) succeeded in {Ms}ms — tokens {Prompt}/{Completion}",
                    provider.Name, provider.Model,
                    (int)(DateTime.UtcNow - started).TotalMilliseconds,
                    result.PromptTokens, result.CompletionTokens);
                return outcome;
            }

            var retryable = IsRetryable(outcome.Error!);
            _logger.LogWarning(
                "AI call via {Provider} failed (attempt {Attempt}/{Max}, retryable: {Retryable})",
                provider.Name, attempt, maxAttempts, retryable);

            if (!retryable || attempt == maxAttempts)
            {
                return outcome;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), ct);
        }

        return AiOutcome<AiChatResult>.Fail($"AI provider '{provider.Name}' failed");
    }

    private static bool IsRetryable(string error) =>
        error.Contains("returned 429", StringComparison.Ordinal) ||
        error.Contains("returned 5", StringComparison.Ordinal) ||
        error.Contains("timed out", StringComparison.Ordinal) ||
        error.Contains("unreachable", StringComparison.Ordinal);

    private IAiProvider? ResolveAdapter(ProviderOptions provider)
    {
        var httpClientFactory = _services.GetRequiredHttpClientFactory();
        var loggerFactory = _services.GetRequiredLoggerFactory();

        var http = httpClientFactory.CreateClient("Ai:" + provider.Name);
        http.Timeout = TimeSpan.FromSeconds(Math.Max(5, provider.TimeoutSeconds));

        return provider.Protocol.Trim().ToLowerInvariant() switch
        {
            ProviderOptions.ProtocolAnthropic => new Providers.AnthropicProvider(
                http, provider, loggerFactory.CreateLogger<Providers.AnthropicProvider>()),
            ProviderOptions.ProtocolGemini => new Providers.GeminiProvider(
                http, provider, loggerFactory.CreateLogger<Providers.GeminiProvider>()),
            _ => new Providers.OpenAiCompatibleProvider(
                http, provider, loggerFactory.CreateLogger<Providers.OpenAiCompatibleProvider>()),
        };
    }
}

/// <summary>Narrow accessors so the factory stays testable without the full host.</summary>
public static class ServiceProviderAiExtensions
{
    public static IHttpClientFactory GetRequiredHttpClientFactory(this IServiceProvider services) =>
        services.GetRequiredService<IHttpClientFactory>();

    public static ILoggerFactory GetRequiredLoggerFactory(this IServiceProvider services) =>
        services.GetRequiredService<ILoggerFactory>();
}

