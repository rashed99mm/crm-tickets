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
        var candidate = ExtractJsonObject(json);
        if (candidate is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(candidate);
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
    /// Real models routinely ignore "JSON only" and wrap the answer in a ```json fence, or add a
    /// sentence of preamble/trailing commentary, even when explicitly told not to. This extracts
    /// the first balanced <c>{...}</c> object from the raw text so a well-formed answer still
    /// parses despite the wrapping — the parse itself stays strict (an unbalanced or malformed
    /// object still returns <c>null</c>), so the AI-36 "never a best-effort guess" invariant holds:
    /// this widens what counts as *extractable*, not what counts as *valid*.
    /// </summary>
    private static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
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
