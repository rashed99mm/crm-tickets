using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Localization;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using FluentValidation;
using MediatR;
using System.Net;
using System.Text.Json;

namespace CustomerSupport.Api.Shared.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IMessageFactory messageFactory)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, messageFactory);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, IMessageFactory messageFactory)
    {
        var response = exception switch
        {
            ValidationException validationEx => BuildValidationResponse(messageFactory, validationEx),
            UnauthorizedAccessException => messageFactory.Fail<Unit>(ApplicationErrors.General.UNAUTHORIZED, MessageType.Unauthorized),
            ArgumentException => messageFactory.Fail<Unit>(ApplicationErrors.General.BAD_REQUEST, MessageType.Validation),
            KeyNotFoundException => messageFactory.Fail<Unit>(ApplicationErrors.General.RESOURCE_NOT_FOUND, MessageType.NotFound),
            _ => messageFactory.Fail<Unit>(ApplicationErrors.General.INTERNAL_ERROR, MessageType.Internal)
        };

        _logger.LogError(exception, "Error handling request: {Message}", exception.Message);

        var statusCode = exception switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ArgumentException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        // AC-53: every response carries a trace id that correlates with the server log. The failure
        // factory above has no HttpContext, so it is stamped here at the one place that has one —
        // the same arrangement AuthorizationEnvelopeMiddleware uses for 401/403 envelopes.
        var envelope = new
        {
            response.Success,
            response.Code,
            response.Message,
            response.Data,
            response.Errors,
            traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier,
            response.Timestamp,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    private static Response<Unit> BuildValidationResponse(IMessageFactory messageFactory, ValidationException validationEx)
    {
        var fieldErrors = validationEx.Errors
            .GroupBy(e => e.PropertyName)
            .Select(g => new FieldError(
                g.Key,
                SystemCodeMap.Resolve(g.Key),
                g.First().ErrorMessage))
            .ToList();

        return messageFactory.Validation<Unit>(ApplicationErrors.General.VALIDATION_ERROR, fieldErrors);
    }
}
