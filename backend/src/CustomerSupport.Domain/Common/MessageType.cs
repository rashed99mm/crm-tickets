using System.Text.Json.Serialization;

namespace CustomerSupport.Domain.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageType
{
    None,
    Success,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    BusinessRule,
    PayloadTooLarge,
    UnsupportedMediaType,
    Internal
}
