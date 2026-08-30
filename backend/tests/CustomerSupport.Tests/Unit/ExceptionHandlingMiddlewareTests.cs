using System.Net;
using System.Text.Json;
using CustomerSupport.Api.Shared.Middleware;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

/// <summary>
/// US-123 / AC-53's second half: an unhandled exception must become a generic 500 envelope with a
/// trace id, and the body must not echo the exception. Driven at the middleware directly rather
/// than through a diagnostic route, because shipping a test-only "make me fail" endpoint into a
/// production host is exactly the kind of seam this story exists to prevent.
///
/// The other arms (validation, unauthorized, argument, key-not-found) are proven end-to-end by
/// `Integration/ContractHardeningTests.cs`; only the `_` arm lacked any driver.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(HttpResponseMessage Response, string Body)> InvokeWithAsync(
        Exception thrown)
    {
        var response = new HttpResponseMessage();
        var context = new DefaultHttpContext();

        var body = new MemoryStream();
        context.Response.Body = body;

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw thrown,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        // IMessageFactory is resolved per request; substitute the real one over no-op dependencies.
        await middleware.InvokeAsync(context, new StubMessageFactory());

        body.Position = 0;
        var payload = JsonSerializer.Deserialize<Response<object>>(
            await new StreamReader(body).ReadToEndAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return (
            new HttpResponseMessage((HttpStatusCode)context.Response.StatusCode),
            $"{payload?.Code}|{payload?.Message}|{payload?.TraceId}");
    }

    [Fact]
    public async Task AC53_UnhandledException_Returns500WithGenericEnvelope()
    {
        var (status, body) = await InvokeWithAsync(new InvalidOperationException("SECRET-DATABASE-TABLE"));

        status.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().Contain(SystemCode.ERR005);
        body.Should().NotContain("SECRET-DATABASE-TABLE");
    }

    /// <summary>
    /// AC-52 â€” connection strings are the highest-value leak; a hostile exception that embeds one
    /// must still be reduced to the generic code and message.
    /// </summary>
    [Fact]
    public async Task AC52_ExceptionCarryingAConnectionString_LeaksNothing()
    {
        var hostile = new InvalidOperationException(
            "Server=(localdb)\\MSSQLLocalDB;Database=CustomerSupportCrm;Trusted_Connection=True");

        var (status, body) = await InvokeWithAsync(hostile);

        status.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContain("CustomerSupportCrm");
        body.Should().NotContain("Trusted_Connection");
        body.Should().Contain(SystemCode.ERR005);
    }

    /// <summary>AC-53 â€” every response carries something to correlate against the server log.</summary>
    [Fact]
    public async Task AC53_UnhandledException_CarriesATraceId()
    {
        var (_, body) = await InvokeWithAsync(new InvalidOperationException());

        var traceField = body.Split('|')[^1];
        traceField.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The production `MessageFactory` resolves localized text through `ILocalizationService`; the
    /// code path under test only consumes code/message/traceId, so this stub returns that shape via
    /// the same `Response` factories production uses.
    /// </summary>
    private sealed class StubMessageFactory : IMessageFactory
    {
        public Response<T> Success<T>(T data, string domainKey) =>
            Response<T>.Ok(data, SystemCodeMap.Resolve(domainKey), domainKey);

        public Response<T> Fail<T>(string domainKey, MessageType type) =>
            Response<T>.Fail(SystemCodeMap.Resolve(domainKey), type.ToString(), type);

        public Response<T> Fail<T>(string domainKey, MessageType type, IList<FieldError> errors) =>
            Response<T>.Fail(SystemCodeMap.Resolve(domainKey), type.ToString(), type, errors);

        public Response<T> NotFound<T>(string domainKey) => Fail<T>(domainKey, MessageType.NotFound);

        public Response<T> Validation<T>(string domainKey, IList<FieldError> errors) =>
            Response<T>.Fail(SystemCodeMap.Resolve(domainKey), "Validation", MessageType.Validation, errors);
    }
}
