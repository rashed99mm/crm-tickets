using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Common.Options;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Ai.Providers;

/// <summary>
/// The plumbing every wire protocol shares: one POST per call, timeout mapped to a failure
/// outcome rather than an exception (A6), and error outcomes that carry an HTTP status plus a
/// truncation-safe provider snippet — never request material or credentials (A3, AI-34).
/// </summary>
public abstract class ProviderHttpBase : IAiProvider
{
    protected static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    protected readonly HttpClient Http;
    protected readonly ProviderOptions Options;
    protected readonly ILogger Logger;

    protected ProviderHttpBase(HttpClient http, ProviderOptions options, ILogger logger)
    {
        Http = http;
        Options = options;
        Logger = logger;
    }

    public string Name => Options.Name;

    public bool IsConfigured => Options.IsConfigured;

    public async Task<AiOutcome<AiChatResult>> CompleteAsync(AiChatRequest request, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return AiOutcome<AiChatResult>.Fail("AI provider is not configured");
        }

        try
        {
            return await SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Logger.LogWarning("AI provider {Provider} timed out after {Seconds}s",
                Name, Options.TimeoutSeconds);
            return AiOutcome<AiChatResult>.Fail($"AI provider '{Name}' timed out");
        }
        catch (HttpRequestException ex)
        {
            Logger.LogWarning(ex, "AI provider {Provider} transport failure", Name);
            return AiOutcome<AiChatResult>.Fail($"AI provider '{Name}' is unreachable");
        }
    }

    /// <summary>Protocol-specific request/response. Implemented by each adapter.</summary>
    protected abstract Task<AiOutcome<AiChatResult>> SendAsync(AiChatRequest request, CancellationToken ct);

    protected async Task<AiOutcome<AiChatResult>> SendCoreAsync(
        HttpRequestMessage message,
        Func<JsonDocument, AiChatResult?> parse,
        CancellationToken ct)
    {
        using var response = await Http.SendAsync(message, ct);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("AI provider {Provider} answered {Status}", Name, (int)response.StatusCode);
            return AiOutcome<AiChatResult>.Fail(
                $"AI provider '{Name}' returned {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var body = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var result = parse(body);

        return result is null || string.IsNullOrWhiteSpace(result.Text)
            ? AiOutcome<AiChatResult>.Fail($"AI provider '{Name}' returned an empty completion")
            : AiOutcome<AiChatResult>.Ok(result);
    }

    protected static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
