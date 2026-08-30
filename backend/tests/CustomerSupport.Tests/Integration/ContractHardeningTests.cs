using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-09 — the cross-cutting pass. `AC-51`, `AC-52`, `AC-53`, `AC-54`.
///
/// These criteria are continuous obligations that every feature from FEAT-02 onward was expected to
/// satisfy. Nothing here introduces the behaviour; it audits the whole surface at once and turns
/// "we believe every endpoint does this" into a test that fails when one stops.
/// </summary>
public class ContractHardeningTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_client, _) = await _factory.CreateAuthenticatedClientAsync("Supervisor");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private static bool IsEnvelope(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("success", out _)
        && root.TryGetProperty("code", out _)
        && root.TryGetProperty("message", out _);

    // --- US-122 — a stable code per condition -----------------------------------------------------

    /// <summary>
    /// Every GET route the host registers, driven once and checked for the envelope. Routes with
    /// parameters are skipped — they need fixtures, and the feature suites already exercise them.
    /// </summary>
    [Fact]
    [Trait("AC", "51")]
    public async Task AC51_EveryEndpoint_AnswersInTheEnvelope()
    {
        var routes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true)
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .Where(p => p.StartsWith("api/", StringComparison.OrdinalIgnoreCase) && !p.Contains('{'))
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        routes.Should().NotBeEmpty("the audit is worthless if it inspects nothing");

        var offenders = new List<string>();

        foreach (var route in routes)
        {
            var response = await _client.GetAsync("/" + route);
            var body = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                offenders.Add($"{route} -> empty body ({(int)response.StatusCode})");
                continue;
            }

            try
            {
                if (!IsEnvelope(JsonDocument.Parse(body).RootElement))
                {
                    offenders.Add($"{route} -> not an envelope: {body[..Math.Min(120, body.Length)]}");
                }
            }
            catch (JsonException)
            {
                offenders.Add($"{route} -> not JSON: {body[..Math.Min(120, body.Length)]}");
            }
        }

        output.WriteLine($"audited {routes.Count} parameterless GET routes");
        offenders.Should().BeEmpty();
    }

    /// <summary>
    /// A failure with an empty message is a code the user cannot act on — US-107's complaint, and
    /// the exact defect that shipped unnoticed when Resources.yaml was not copied to the output.
    /// </summary>
    [Fact]
    [Trait("AC", "51")]
    public async Task AC51_EveryFailure_CarriesACodeAndBothLanguages()
    {
        var failures = new List<HttpResponseMessage>
        {
            // Validation
            await _client.PostAsJsonAsync("/api/Customers", new { name = "", email = "bad" }),
            // Not found
            await _client.GetAsync($"/api/Customers/{Guid.NewGuid()}"),
            // Conflict
            await CreateDuplicateCustomerAsync(),
        };

        foreach (var response in failures)
        {
            var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

            var code = root.GetProperty("code").GetString();
            var message = root.GetProperty("message").GetString();

            code.Should().NotBeNullOrWhiteSpace();
            message.Should().NotBeNullOrWhiteSpace();

            // The failure mode that shipped once: the catalogue lookup misses and the code is
            // echoed back as its own message.
            message.Should().NotBe(code, $"'{code}' has no message in Resources.yaml");
        }
    }

    /// <summary>
    /// Catalogue completeness, checked against the constants rather than against whatever the tests
    /// happen to trigger. This is the test that would have caught the missing-YAML defect.
    /// </summary>
    [Fact]
    [Trait("AC", "51")]
    public void EveryErrorCode_HasABilingualMessage()
    {
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Localization", "Resources.yaml"));

        var codes = typeof(ApplicationErrors).GetNestedTypes()
            .SelectMany(t => t.GetFields())
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Distinct()
            .ToList();

        codes.Should().NotBeEmpty();

        var missing = codes.Where(c => !yaml.Contains(c + ":", StringComparison.Ordinal)).ToList();

        output.WriteLine($"{codes.Count} codes declared, {missing.Count} without a catalogue entry");
        foreach (var m in missing)
        {
            output.WriteLine("  missing: " + m);
        }

        missing.Should().BeEmpty("every code must resolve to a message (US-107)");
    }

    // --- US-123 — diagnosable without leaking ------------------------------------------------------

    /// <summary>
    /// Driven into a real failure rather than through a test-only throwing endpoint, which would be
    /// a route that ships. A malformed base64 rowVersion reaches code that will try to decode it.
    /// </summary>
    [Fact]
    [Trait("AC", "52")]
    public async Task AC52_AFailingRequest_LeaksNoInternals()
    {
        var probes = new List<HttpResponseMessage>
        {
            await _client.GetAsync($"/api/Customers/{Guid.NewGuid()}"),
            await _client.PostAsJsonAsync("/api/Customers", new { name = "", email = "bad" }),
            await _client.PostAsJsonAsync($"/api/Tickets/{Guid.NewGuid()}/status",
                new { status = "Open", rowVersion = "AAAAAAABAdE=" }),
        };

        string[] forbidden =
        [
            "   at ",                     // stack frame
            "System.",                    // CLR type names
            "Microsoft.EntityFrameworkCore",
            "SELECT ",
            "INSERT ",
            "Server=",                    // connection string
            "Password=",
            "Trusted_Connection",
            "MSSQLLocalDB",
        ];

        foreach (var response in probes)
        {
            var body = await response.Content.ReadAsStringAsync();
            foreach (var needle in forbidden)
            {
                body.Should().NotContain(needle,
                    $"a response body must not leak internals (AC-52); body was: {body}");
            }
        }
    }

    [Fact]
    [Trait("AC", "52")]
    public async Task AC52_UnknownRoute_ReturnsTheEnvelopeNotAnHtmlPage()
    {
        var response = await _client.GetAsync("/api/DefinitelyNotARoute");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("<html", "an HTML error page is a leak and an inconsistency");
    }

    [Fact]
    [Trait("AC", "52")]
    public async Task AC52_MalformedJson_ReturnsEnvelopeWithoutParserDetail()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Customers")
        {
            Content = new StringContent("{ this is not json", Encoding.UTF8, "application/json"),
        };

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotContain("   at ");
        body.Should().NotContain("System.Text.Json");
    }

    /// <summary>
    /// AC-53. Recorded expectation before running: the frontend's ApiError carries `traceId: ''`
    /// with a comment saying the backend's Result&lt;T&gt; has no such field. If that is still true
    /// this fails, and the criterion is reported as not met rather than adjusted around.
    /// </summary>
    [Fact]
    [Trait("AC", "53")]
    public async Task AC53_Responses_CarryATraceIdentifier()
    {
        var response = await _client.GetAsync($"/api/Customers/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;

        var inEnvelope = root.TryGetProperty("traceId", out var t) && !string.IsNullOrWhiteSpace(t.GetString());
        var inHeader = response.Headers.Contains("X-Trace-Id")
                       || response.Headers.Contains("traceparent")
                       || response.Headers.Contains("Request-Id");

        output.WriteLine($"traceId in envelope: {inEnvelope}; correlation header: {inHeader}");
        output.WriteLine("body: " + body);

        (inEnvelope || inHeader).Should().BeTrue(
            "AC-53 requires a trace identifier a caller can quote back to support");
    }

    // --- US-124 — unambiguous wire format ----------------------------------------------------------

    [Fact]
    [Trait("AC", "54")]
    public async Task AC54_ResponseProperties_AreCamelCase()
    {
        var response = await _client.GetAsync("/api/Categories");
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        foreach (var property in root.EnumerateObject())
        {
            char.IsLower(property.Name[0]).Should().BeTrue($"'{property.Name}' is not camelCase");
        }

        var first = root.GetProperty("data").EnumerateArray().First();
        foreach (var property in first.EnumerateObject())
        {
            char.IsLower(property.Name[0]).Should().BeTrue($"'{property.Name}' is not camelCase");
        }
    }

    /// <summary>
    /// AC-54 says "ISO 8601 UTC". A live probe showed `"2026-08-25T22:16:20.9707248"` — ISO 8601 in
    /// shape but with **no timezone designator**, which a client parses as local time. This test is
    /// expected to fail before it passes.
    /// </summary>
    [Fact]
    [Trait("AC", "54")]
    public async Task AC54_DatesOnTheWire_AreIso8601Utc()
    {
        var create = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Wire Format",
            email = $"wire-{Guid.NewGuid():N}@example.com",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;

        var body = await _client.GetStringAsync($"/api/Customers/{id}");
        var createdAt = JsonDocument.Parse(body).RootElement
            .GetProperty("data").GetProperty("createdAt").GetString();

        output.WriteLine("createdAt on the wire: " + createdAt);

        createdAt.Should().NotBeNullOrWhiteSpace();
        createdAt.Should().MatchRegex(@"(Z|[+-]\d{2}:\d{2})$",
            "AC-54 requires ISO 8601 UTC; without an offset a client parses it as local time");
    }

    private async Task<HttpResponseMessage> CreateDuplicateCustomerAsync()
    {
        var email = $"dupe-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/Customers", new { name = "First", email });
        return await _client.PostAsJsonAsync("/api/Customers", new { name = "Second", email });
    }
}
