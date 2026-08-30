using System.Text.Json;

namespace CustomerSupport.Application.Ai;

/// <summary>
/// Strict JSON parsing for schema-told answers (AI-36).
///
/// Lives in Application so feature handlers can call it without breaking the dependency rule.
/// The provider implementation in <c>CustomerSupport.Infrastructure.Ai.ResilientAiService</c>
/// re-uses the same helpers — both layers are read-only consumers of a pure parser, so a
/// shared <c>Application</c>-level helper is the smallest place that satisfies both.
/// </summary>
public static class AiJson
{
    public static List<string>? ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return items.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// AC-21.11 — read one of the three sentiment labels from a schema-told JSON document shaped
    /// <c>{"items":["..."]}</c>. Anything else — case mismatch, unknown label, malformed JSON,
    /// null/whitespace input — returns <c>null</c> so the caller can render the summary without
    /// a chip rather than crash.
    /// </summary>
    public static string? ParseSentiment(string? json)
    {
        var label = ParseStringArray(json)?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return label switch
        {
            "Frustrated" or "Neutral" or "Satisfied" => label,
            _ => null,
        };
    }
}
