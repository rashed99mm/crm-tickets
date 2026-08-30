using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Common.Options;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Ai.Providers;

/// <summary>
/// AI-30 — the OpenAI-schema <c>/chat/completions</c> adapter. One implementation serves OpenAI,
/// Azure OpenAI, OpenRouter, Groq, Mistral and Ollama, because they share the wire format the
/// platform has always spoken; the base URL and model id remain configuration.
/// </summary>
public class OpenAiCompatibleProvider : ProviderHttpBase
{
    public OpenAiCompatibleProvider(HttpClient http, ProviderOptions options, ILogger logger)
        : base(http, options, logger)
    {
        Http.BaseAddress = new Uri(Options.BaseUrl.TrimEnd('/') + "/");
        Http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Options.ApiKey);
    }

    protected override async Task<AiOutcome<AiChatResult>> SendAsync(AiChatRequest request, CancellationToken ct)
    {
        var payload = new
        {
            model = Options.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions),
                Encoding.UTF8, "application/json"),
        };

        return await SendCoreAsync(message, body =>
        {
            var text = body.RootElement
                .TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content)
                    ? content.GetString()?.Trim()
                    : null;

            if (text is null)
            {
                return null;
            }

            var usage = body.RootElement.TryGetProperty("usage", out var u) ? u : default;
            return new AiChatResult(
                text,
                usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("prompt_tokens", out var p)
                    ? p.GetInt32() : 0,
                usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("completion_tokens", out var c)
                    ? c.GetInt32() : 0);
        }, ct);
    }
}
