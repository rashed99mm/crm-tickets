using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomerSupport.Api.Shared.Serialization;

/// <summary>
/// Writes every <see cref="DateTime"/> with an explicit UTC designator — `AC-54`.
/// </summary>
/// <remarks>
/// Every timestamp this system stores is UTC: entities set <c>DateTime.UtcNow</c> and the schema
/// documents it. But EF returns <see cref="DateTimeKind.Unspecified"/> after a round trip, so
/// <c>System.Text.Json</c> writes no <c>Z</c> — and a value like
/// <c>2026-08-25T22:58:48.9296923</c> is parsed as **local** time by every browser. An agent in
/// Cairo would have seen every timestamp shifted by three hours, silently.
///
/// A serialization converter rather than an EF value converter: an EF converter would fix values
/// read through EF but not ones a handler computes in memory, and it would have to be applied to
/// every entity. The wire format is a serialization concern.
/// </remarks>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    internal const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToUtc(value).ToString(Format, CultureInfo.InvariantCulture));

    /// <summary>
    /// <c>Unspecified</c> is treated as UTC rather than converted from local. Converting would
    /// apply the server's offset to a value that never had one, which is the bug this converter
    /// exists to prevent, in the opposite direction.
    /// </summary>
    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime(),
    };
}

/// <summary>The nullable half. Without it a <c>DateTime?</c> bypasses the converter entirely.</summary>
public sealed class UtcNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null
            ? null
            : DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(
            UtcDateTimeConverter.ToUtc(value.Value)
                .ToString(UtcDateTimeConverter.Format, CultureInfo.InvariantCulture));
    }
}
