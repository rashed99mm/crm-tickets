using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Contracts;

public sealed record Response<T>
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IList<FieldError> Errors { get; init; } = [];
    public string? TraceId { get; init; }
    public DateTimeOffset Timestamp { get; init; }

    public static Response<T> Ok(T data, string code, string message) => new()
    {
        Success = true,
        Code = code,
        Message = message,
        Data = data,
        Timestamp = DateTimeOffset.UtcNow
    };

    public static Response<T> Fail(string code, string message, MessageType type, IList<FieldError>? errors = null) => new()
    {
        Success = false,
        Code = code,
        Message = message,
        Errors = errors ?? [],
        Timestamp = DateTimeOffset.UtcNow
    };
}

public static class Response
{
    public static Response<Unit> Ok(string code, string message) => Response<Unit>.Ok(Unit.Value, code, message);
    public static Response<Unit> Fail(string code, string message, MessageType type, IList<FieldError>? errors = null)
        => Response<Unit>.Fail(code, message, type, errors);
}
