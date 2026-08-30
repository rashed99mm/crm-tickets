using System.Text;
using System.Text.Json;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Common.Options;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Ai.Providers;

/// <summary>
/// AI-30 — Anthropic's Messages API. Different auth header (<c>x-api-key</c>), different
/// body (system is a top-level field, not a message), different usage names — which is exactly
/// why it gets its own adapter instead of being bent into the OpenAI schema.
/// </summary>
public class AnthropicProvider : ProviderHttpBase
{
    public AnthropicProvider(HttpClient http, ProviderOptions options, ILogger logger)
        : base(http, options, logger)
    {
        Http.BaseAddress = new Uri(Options.BaseUrl.TrimEnd('/') + "/");
        Http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", Options.ApiKey);
        Http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
    }

    protected override async Task<AiOutcome<AiChatResult>> SendAsync(AiChatRequest request, CancellationToken ct)
    {
        var system = string.Join("\n", request.Messages
            .Where(m => m.Role == "system")
            .Select(m => m.Content));
        var turns = request.Messages
            .Where(m => m.Role != "system")
            .Select(m => new { role = m.Role, content = m.Content })
            .ToList();

        var payload = new
        {
            model = Options.Model,
            system = string.IsNullOrEmpty(system) ? null : system,
            messages = turns,
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions),
                Encoding.UTF8, "application/json"),
        };

        return await SendCoreAsync(message, body =>
        {
            var root = body.RootElement;
            if (!root.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array ||
                content.GetArrayLength() == 0)
            {
                return null;
            }

            var text = string.Concat(content.EnumerateArray()
                .Where(block => block.TryGetProperty("type", out var t) &&
                                t.GetString() == "text" &&
                                block.TryGetProperty("text", out _))
                .Select(block => block.GetProperty("text").GetString()));

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var usage = root.TryGetProperty("usage", out var u) ? u : default;
            return new AiChatResult(
                text.Trim(),
                usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("input_tokens", out var i)
                    ? i.GetInt32() : 0,
                usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("output_tokens", out var o)
                    ? o.GetInt32() : 0);
        }, ct);
    }
}
