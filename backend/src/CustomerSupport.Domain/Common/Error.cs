using System.Text.Json.Serialization;

namespace CustomerSupport.Domain.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    BusinessRule,

    /// <summary>AC-23 — 413. The request is well formed and simply too big to accept.</summary>
    PayloadTooLarge,

    /// <summary>AC-24 — 415. The payload's media type is not on the allowlist.</summary>
    UnsupportedMediaType,

    Internal
}

public sealed record Error(
    string Code,
    string MessageAr,
    string MessageEn,
    ErrorType Type = ErrorType.Internal,
    IDictionary<string, string[]>? Details = null);
