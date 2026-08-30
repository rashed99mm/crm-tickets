using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// A per-run <see cref="WebApplicationFactory{TEntryPoint}"/> against a real LocalDB
/// database, matching the running instance verified manually while diagnosing
/// <see cref="ChangePasswordEndpointTests"/>. Not the in-memory provider: password
/// hashing, Identity's own validation and the real EF query this feature exercises
/// (<c>RevokeAllUserRefreshTokensAsync</c>) all need the real provider to mean anything.
/// </summary>
public sealed class ChangePasswordApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Server=(localdb)\\MSSQLLocalDB;Database=CustomerSupportCrmTest;Trusted_Connection=True;TrustServerCertificate=True");
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-characters-long");
        // ConfigureMessaging reads these two raw config keys directly (not
        // IWebHostEnvironment), so UseEnvironment alone does not satisfy it.
        builder.UseSetting("Messaging:Required", "false");
        builder.UseEnvironment("Development");
    }

    /// <summary>Creates a fresh user directly through Identity, bypassing the API,
    /// so each test owns a user no other test can affect.</summary>
    public async Task<(ApplicationUser User, string Password)> CreateTestUserAsync()
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"changepwd-{Guid.NewGuid():N}@test.local";
        const string password = "Test-Password-456";

        var user = ApplicationUser.Create(email, email, "Test", "User");
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(
            string.Join(", ", result.Errors.Select(e => e.Description)));

        return (user, password);
    }

    public async Task<string> SignInAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<Response<LoginData>>();
        return body!.Data!.AccessToken;
    }

    public async Task<Response<LoginData>> SignInFullAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Response<LoginData>>())!;
    }

    public sealed record LoginData(string AccessToken, string RefreshToken);
}

public class ChangePasswordEndpointTests : IAsyncLifetime
{
    private readonly ChangePasswordApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        // The host seeds ticket categories on start, so the schema has to be current before the
        // factory builds anything — see TestDatabase.
        await TestDatabase.EnsureMigratedAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task ChangePassword_CorrectCurrentPassword_Returns200AndNewPasswordAuthenticates()
    {
        var (user, oldPassword) = await _factory.CreateTestUserAsync();
        var token = await _factory.SignInAsync(_client, user.Email!, oldPassword);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = oldPassword,
                newPassword = "Rotated-Password-9",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The new password authenticates.
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/Auth/login",
            new { email = user.Email, password = "Rotated-Password-9" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400KeyedToCurrentPassword()
    {
        var (user, correctPassword) = await _factory.CreateTestUserAsync();
        var token = await _factory.SignInAsync(_client, user.Email!, correctPassword);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = "definitely-wrong",
                newPassword = "Rotated-Password-9",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        // ErrorType.Validation maps to 400 (ResultActionResultExtensions.MapFailureStatusCode).
        // This test asserted 422 until ADR-0011: the reference platform used 422, but AC-8, AC-11,
        // AC-30, AC-31 and AC-51 all name 400, and AC-38 needs 400 to mean "malformed" for its
        // 409-not-400 contrast to say anything.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(SystemCode.ERR028);
        body.Errors.Should().Contain(e => e.Field == "currentPassword");
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_Returns400KeyedToNewPassword()
    {
        var (user, correctPassword) = await _factory.CreateTestUserAsync();
        var token = await _factory.SignInAsync(_client, user.Email!, correctPassword);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = correctPassword, newPassword = "weak" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR029);
        body.Errors.Should().Contain(e => e.Field == "newPassword");
    }

    [Fact]
    public async Task ChangePassword_Success_RevokesRefreshTokens_OldRefreshTokenFails()
    {
        var (user, oldPassword) = await _factory.CreateTestUserAsync();
        var signIn = await _factory.SignInFullAsync(_client, user.Email!, oldPassword);
        var oldRefreshToken = signIn.Data!.RefreshToken;
        var accessToken = signIn.Data!.AccessToken;

        var changeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = oldPassword,
                newPassword = "Rotated-Password-9",
            }),
        };
        changeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var changeResponse = await _client.SendAsync(changeRequest);
        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/Auth/refresh",
            new { accessToken, refreshToken = oldRefreshToken });

        // The old refresh token was revoked by the password change, so it must not
        // still work.
        refreshResponse.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassword_NoToken_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = "whatever",
                newPassword = "Rotated-Password-9",
            }),
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
