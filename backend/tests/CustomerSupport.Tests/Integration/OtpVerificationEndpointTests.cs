using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Verification;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// The OTP verify endpoint over the wire (AC-439..AC-445). Records are seeded directly through the
/// repository with a real code hash, so the tests prove the HTTP contract, authorization scoping
/// and the Identity confirmation flip without depending on the (separate) OTP request flow.
/// </summary>
public sealed class OtpVerificationEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;
    private ApplicationUser _user = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_client, _user) = await _factory.CreateAuthenticatedClientAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private sealed record VerifyResult(bool Verified, string Type);
    private sealed record VerifyResponse(bool Success, string Code, VerifyResult Data);

    private const string ValidCode = "123456";

    private async Task<OtpVerification> SeedVerificationAsync(
        Guid userId, OtpVerificationType type, string? code = ValidCode, int failedAttempts = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOtpVerificationRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IOtpCodeHasher>();

        var verification = OtpVerification.Create(
            userId,
            type == OtpVerificationType.Phone ? "+14155550100" : "owner@test.local",
            type,
            hasher.Hash(code!),
            DateTime.UtcNow.AddMinutes(5),
            DateTime.UtcNow);

        for (var i = 0; i < failedAttempts; i++)
        {
            verification.RegisterFailedAttempt();
        }

        await repo.AddAsync(verification, default);
        return verification;
    }

    private async Task<HttpResponseMessage> VerifyAsync(Guid verificationId, string code) =>
        await _client.PostAsJsonAsync("/api/verification/verify", new { verificationId, code });

    [Fact]
    public async Task Verify_CorrectPhoneCode_ConfirmsPhoneAndReturnsSuccess() // AC-439
    {
        var verification = await SeedVerificationAsync(_user.Id, OtpVerificationType.Phone);

        var response = await VerifyAsync(verification.Id, ValidCode);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VerifyResponse>();
        body!.Success.Should().BeTrue();
        body.Data.Verified.Should().BeTrue();
        body.Data.Type.Should().Be(nameof(OtpVerificationType.Phone));

        (await GetUserPhoneConfirmedAsync(_user.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Verify_WrongCode_DoesNotConfirmPhone() // AC-440
    {
        var verification = await SeedVerificationAsync(_user.Id, OtpVerificationType.Phone);

        var response = await VerifyAsync(verification.Id, "000000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetUserPhoneConfirmedAsync(_user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Verify_MalformedCode_ReturnsSafeFailure() // AC-440
    {
        var verification = await SeedVerificationAsync(_user.Id, OtpVerificationType.Phone);

        var response = await VerifyAsync(verification.Id, "abc");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Verify_UnknownId_ReturnsSafeFailure() // AC-443
    {
        var response = await VerifyAsync(Guid.NewGuid(), ValidCode);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Verify_OtherUsersRecord_ReturnsSafeFailure() // AC-443
    {
        var (otherClient, otherUser) = await _factory.CreateAuthenticatedClientAsync();
        var verification = await SeedVerificationAsync(otherUser.Id, OtpVerificationType.Phone);

        // Call with the FIRST user's token, not the owner's.
        var response = await _client.PostAsJsonAsync("/api/verification/verify",
            new { verificationId = verification.Id, code = ValidCode });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Verify_LockedAfterFiveFailures_StaysUnconfirmed() // AC-441
    {
        var verification = await SeedVerificationAsync(_user.Id, OtpVerificationType.Phone, failedAttempts: 5);

        for (var i = 0; i < 2; i++)
        {
            var response = await VerifyAsync(verification.Id, "000000");
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        (await GetUserPhoneConfirmedAsync(_user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ResponseNeverContainsSecrets() // AC-445
    {
        var verification = await SeedVerificationAsync(_user.Id, OtpVerificationType.Phone);

        var response = await VerifyAsync(verification.Id, ValidCode);
        var json = await response.Content.ReadAsStringAsync();

        json.Should().NotContain(ValidCode);
        json.Should().NotContain("codeHash", "the stored hash must never leave the server");
        json.Should().NotContain("123456");
    }

    private async Task<bool> GetUserPhoneConfirmedAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user!.PhoneNumberConfirmed;
    }
}
