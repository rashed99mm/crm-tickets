using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Verification;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// The OTP <em>request</em> endpoints over the wire (OTP-1, OTP-2, OTP-3, OTP-9). A recording fake
/// <see cref="INotificationGateway"/> is swapped in so the tests prove the controller, mediator,
/// handler and EF persistence chain end-to-end without a provider dependency.
/// </summary>
public sealed class OtpRequestEndpointTests : IAsyncLifetime
{
    private readonly FakeGatewayApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_client, var user) = await _factory.CreateAuthenticatedClientAsync();
        _userId = user.Id;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private sealed record OtpRequestData(Guid VerificationId, DateTime ExpiresAtUtc, int RetryAfterSeconds, string Channel);
    private sealed record OtpRequestResponse(bool Success, string Code, string Message, OtpRequestData? Data);

    private async Task<OtpVerification?> StoredAsync(string contact, OtpVerificationType type)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOtpVerificationRepository>();
        return await repo.GetLatestForUserAsync(_userId, contact, type, default);
    }

    private async Task<HttpResponseMessage> RequestAsync(object body) =>
        await _client.PostAsJsonAsync("/api/verification/request", body);

    private async Task<HttpResponseMessage> RequestPhoneAsync(string phoneNumber) =>
        await _client.PostAsJsonAsync("/api/verification/request-phone", new { phoneNumber });

    [Fact]
    public async Task Request_Email_DispatchesViaEmailChannelAndPersistsHashedRecord() // OTP-1
    {
        var response = await RequestAsync(new { contact = " Owner@Test.LOCAL ", type = "Email" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OtpRequestResponse>();
        body!.Success.Should().BeTrue();
        body.Data!.VerificationId.Should().NotBeEmpty();
        body.Data.Channel.Should().Be("Email");
        body.Data.RetryAfterSeconds.Should().Be(60);
        body.Data.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(10));

        var dispatch = _factory.Gateway.Dispatched.Should().ContainSingle().Which;
        dispatch.Channels.Should().ContainSingle(c => c == NotificationChannel.Email);
        dispatch.Email.Should().Be("owner@test.local");
        dispatch.Variables["Code"].Should().MatchRegex(@"^\d{6}$");

        var stored = await StoredAsync("owner@test.local", OtpVerificationType.Email);
        stored.Should().NotBeNull();
        stored!.Id.Should().Be(body.Data.VerificationId);
        stored.IsVerified.Should().BeFalse();
        stored.IsInvalidated.Should().BeFalse();
        stored.CodeHash.Should().NotBe(dispatch.Variables["Code"]);
        stored.CodeHash.Should().NotContain(dispatch.Variables["Code"]);
    }

    [Fact]
    public async Task RequestPhone_DispatchesViaSmsChannelAndPersistsRecord() // OTP-2
    {
        const string phone = "+14155550100";

        var response = await RequestPhoneAsync(phone);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OtpRequestResponse>();
        body!.Success.Should().BeTrue();
        body.Data!.Channel.Should().Be("SMS");

        var dispatch = _factory.Gateway.Dispatched.Should().ContainSingle().Which;
        dispatch.Channels.Should().ContainSingle(c => c == NotificationChannel.Sms);
        dispatch.PhoneNumber.Should().Be(phone);

        var stored = await StoredAsync(phone, OtpVerificationType.Phone);
        stored.Should().NotBeNull();
        stored!.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Request_WithinCooldown_Returns429AndDoesNotResend() // OTP-3
    {
        var first = await RequestPhoneAsync("+14155550100");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await RequestPhoneAsync("+14155550100");

        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await second.Content.ReadFromJsonAsync<OtpRequestResponse>();
        body!.Code.Should().Be("ERR074");
        _factory.Gateway.Dispatched.Should().ContainSingle();
    }

    [Fact]
    public async Task Request_WhenDispatchNotAccepted_PersistsNothing() // OTP-9
    {
        _factory.Gateway.Succeed = false;
        const string phone = "+14155550100";

        var response = await RequestPhoneAsync(phone);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<OtpRequestResponse>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be("ERR073");
        _factory.Gateway.Dispatched.Should().ContainSingle();

        (await StoredAsync(phone, OtpVerificationType.Phone)).Should().BeNull();
    }

    [Fact]
    public async Task Request_Unauthenticated_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/verification/request-phone", new { phoneNumber = "+14155550100" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _factory.Gateway.Dispatched.Should().BeEmpty();
    }
}

/// <summary>Records every dispatch so tests can assert channel, contact and code-variable routing.</summary>
public sealed class RecordingGateway : INotificationGateway
{
    private readonly List<NotificationDispatchRequest> _dispatched = new();

    public bool Succeed { get; set; } = true;

    public IReadOnlyCollection<NotificationDispatchRequest> Dispatched => _dispatched;

    public Task<NotificationDispatchResult> SendAsync(NotificationDispatchRequest request, CancellationToken ct = default)
    {
        _dispatched.Add(request);
        var results = request.Channels.Select(c => new ChannelSendResult(c, Succeed)).ToList();
        return Task.FromResult(new NotificationDispatchResult(Succeed, results));
    }
}

/// <summary>The stock factory with the real gateway replaced by <see cref="RecordingGateway"/>.</summary>
public sealed class FakeGatewayApiFactory : CrmApiFactory
{
    public RecordingGateway Gateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
            services.AddScoped<INotificationGateway>(_ => Gateway));
    }
}