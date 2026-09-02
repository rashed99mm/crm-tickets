using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// CC-47 (as revised) — the portal web form's backend. The valid submission creates a ticket and
/// returns its reference; a honeypot-filled submission and a throttled burst return responses a
/// caller outside the process cannot tell apart from the valid one, while creating nothing.
/// The request/response contract is the portal's, already fixed (spec A20):
/// frontend/projects/common/src/lib/channels/web-form.api.ts.
/// </summary>
public class WebFormSubmissionTests : IAsyncLifetime
{
    private const string Path = "/api/external/webform/submit";
    private readonly CrmExternalApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync().AsTask();
    }

    private Task<HttpResponseMessage> SubmitAsync(
        string email, string subject = "Cannot sign in", string? honeypot = null) =>
        _client.PostAsJsonAsync(Path, new
        {
            name = "Layla Haddad",
            email,
            subject,
            description = "The sign-in page rejects my password.",
            honeypot,
        });

    /// <summary>The envelope's data payload — what portal-app's envelopeInterceptor unwraps to.</summary>
    private static async Task<(string Reference, bool Success)> ReadDataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        return (data.GetProperty("reference").GetString()!, data.GetProperty("success").GetBoolean());
    }

    [Fact]
    [Trait("AC", "CC47")]
    public async Task CC47_ValidSubmission_CreatesAWebFormTicketAndReturnsItsReference()
    {
        var email = $"cc47-valid-{Guid.NewGuid():N}@example.com";

        var response = await SubmitAsync(email);

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"body: {raw[..Math.Min(500, raw.Length)]}");

        var (reference, success) = await ReadDataAsync(response);
        success.Should().BeTrue();
        reference.Should().MatchRegex(@"^TKT-\d{6}$", "the portal shows this to the customer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = await db.Customers.SingleAsync(c => c.Email == email);
        var ticket = await db.Tickets.SingleAsync(t => t.CustomerId == customer.Id);
        ticket.Reference.Should().Be(reference, "the returned reference must be the real one");
        ticket.Source.Should().Be(ChannelNames.WebForm);
        ticket.Subject.Should().Be("Cannot sign in", "A23 — the subject the customer typed");
    }

    [Fact]
    [Trait("AC", "CC47")]
    public async Task CC47_HoneypotFilled_LooksIdenticalAndCreatesNothing()
    {
        var email = $"cc47-honeypot-{Guid.NewGuid():N}@example.com";

        var response = await SubmitAsync(email, honeypot: "http://spam.example.com");

        response.StatusCode.Should().Be(HttpStatusCode.Created, "indistinguishable from a real one");
        var (reference, success) = await ReadDataAsync(response);
        success.Should().BeTrue();
        reference.Should().MatchRegex(@"^TKT-\d{6}$", "a plausible reference, backed by nothing");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.AnyAsync(c => c.Email == email)).Should().BeFalse();
        (await db.Tickets.AnyAsync(t => t.Reference == reference)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC47")]
    public async Task CC47_ThrottledBurst_LooksIdenticalAndCreatesNothing()
    {
        // The throttle is a singleton keyed by remote IP; every request from this client shares one
        // window. PermitLimit submissions succeed, the next is silently refused.
        var emails = Enumerable.Range(0, 7)
            .Select(i => $"cc47-burst-{i}-{Guid.NewGuid():N}@example.com").ToArray();

        var responses = new List<HttpResponseMessage>();
        foreach (var email in emails)
        {
            responses.Add(await SubmitAsync(email));
        }

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created,
            "CC-47: a throttled caller cannot tell the defence fired");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var created = await db.Customers.CountAsync(c => emails.Contains(c.Email));
        created.Should().BeLessThan(emails.Length, "the burst past the limit must create nothing");
    }

    [Fact]
    [Trait("AC", "CC47")]
    public async Task CC47_InvalidEmail_IsAFieldKeyedBadRequest()
    {
        // Validation failure is a real 400 — that is the customer correcting their own typo, not a
        // bot being deflected, and the portal's form renders the field error.
        var response = await _client.PostAsJsonAsync(Path, new
        {
            name = "Layla Haddad",
            email = "not-an-email",
            subject = "Cannot sign in",
            description = "Body",
            honeypot = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
