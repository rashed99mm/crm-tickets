using System.Text;
using System.Text.Json;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Common.Options;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Ai.Providers;

/// <summary>
/// AI-30 — Google's Gemini <c>generateContent</c> API. System instruction is a dedicated field,
/// roles are <c>user</c>/<c>model</c>, and the key travels in the query string.
/// </summary>
public class GeminiProvider : ProviderHttpBase
{
    public GeminiProvider(HttpClient http, ProviderOptions options, ILogger logger)
        : base(http, options, logger)
    {
        // BaseUrl is expected up to /v1beta (default https://generativelanguage.googleapis.com/v1beta).
        Http.BaseAddress = new Uri(Options.BaseUrl.TrimEnd('/') + "/");
    }

    protected override async Task<AiOutcome<AiChatResult>> SendAsync(AiChatRequest request, CancellationToken ct)
    {
        var system = string.Join("\n", request.Messages
            .Where(m => m.Role == "system")
            .Select(m => m.Content));
        var contents = request.Messages
            .Where(m => m.Role != "system")
            .Select(m => new
            {
                role = m.Role == "assistant" ? "model" : "user",
                parts = new[] { new { text = m.Content } },
            })
            .ToList();

        var payload = new
        {
            system_instruction = string.IsNullOrEmpty(system)
                ? null
                : new { parts = new[] { new { text = system } } },
            contents = contents,
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxOutputTokens,
            },
        };

        var path = $"models/{Options.Model}:generateContent?key={Uri.EscapeDataString(Options.ApiKey)}";

        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions),
                Encoding.UTF8, "application/json"),
        };

        return await SendCoreAsync(message, body =>
        {
            var root = body.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                candidates.GetArrayLength() == 0 ||
                !candidates[0].TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
            {
                return null;
            }

            var text = string.Concat(parts.EnumerateArray()
                .Where(p => p.TryGetProperty("text", out _))
                .Select(p => p.GetProperty("text").GetString()));

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var usage = root.TryGetProperty("usageMetadata", out var u) ? u : default;
            return new AiChatResult(
                text.Trim(),
                usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("promptTokenCount", out var p)
                    ? p.GetInt32() : 0,
                usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("candidatesTokenCount", out var c)
                    ? c.GetInt32() : 0);
        }, ct);
    }
}
