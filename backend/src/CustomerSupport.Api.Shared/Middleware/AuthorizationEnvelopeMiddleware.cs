using System.Diagnostics;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Localization;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Api.Shared.Middleware;

/// <summary>
/// Gives a refused request a body — `AC-51`.
/// </summary>
public sealed class AuthorizationEnvelopeMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task InvokeAsync(HttpContext context, IMessageFactory messageFactory)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var refused = context.Response.StatusCode is StatusCodes.Status401Unauthorized
                                                  or StatusCodes.Status403Forbidden;

        if (refused && buffer.Length == 0 && !context.Response.HasStarted)
        {
            await WriteEnvelopeAsync(context, messageFactory);
            return;
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody);
    }

    private static async Task WriteEnvelopeAsync(HttpContext context, IMessageFactory messageFactory)
    {
        var code = context.Response.StatusCode == StatusCodes.Status401Unauthorized
            ? ApplicationErrors.General.UNAUTHORIZED
            : ApplicationErrors.General.FORBIDDEN;

        var type = context.Response.StatusCode == StatusCodes.Status401Unauthorized
            ? MessageType.Unauthorized
            : MessageType.Forbidden;

        var response = messageFactory.Fail<Unit>(code, type);

        context.Response.ContentType = "application/json";

        var envelope = new
        {
            response.Success,
            response.Code,
            response.Message,
            response.Errors,
            traceId = Activity.Current?.Id ?? context.TraceIdentifier,
            response.Timestamp,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, SerializerOptions));
    }
}
