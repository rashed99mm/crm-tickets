using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// Self-service profile update over the wire (AC-430..AC-438). Exercises the real LocalDB database
/// so the Identity update and confirmation-reset behaviour is proven, not assumed.
/// </summary>
public sealed class CurrentUserProfileEndpointTests : IAsyncLifetime
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

    private sealed record ProfileDto(
        Guid Id, string Email, string Username, string FirstName, string LastName,
        string? PhoneNumber, bool EmailConfirmed, bool PhoneNumberConfirmed, bool IsActive,
        DateTime CreatedAt, List<string> Roles);

    private sealed record ApiFailure(bool Success, string Code, string Message, List<FieldErrorDto> Errors);
    private sealed record FieldErrorDto(string Field, string Code, string Message);

    [Fact]
    public async Task PutMe_ValidUpdate_ReturnsUpdatedProfile_AndUnconfirmedPhone() // AC-430, AC-436
    {
        var response = await _client.PutAsJsonAsync("/api/Auth/me", new
        {
            firstName = "Alex",
            lastName = "Morgan",
            phoneNumber = "+14155550100",
            profileImageUrl = "https://cdn.example.test/avatar.png"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Response<ProfileDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.FirstName.Should().Be("Alex");
        body.Data.LastName.Should().Be("Morgan");
        body.Data.PhoneNumber.Should().Be("+14155550100");
        body.Data.PhoneNumberConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task PutMe_Unauthenticated_Returns401() // AC-431
    {
        var anon = _factory.CreateClient();
        var response = await anon.PutAsJsonAsync("/api/Auth/me", new
        {
            firstName = "Alex",
            lastName = "Morgan",
            phoneNumber = (string?)null,
            profileImageUrl = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("", "Morgan")] // AC-433
    [InlineData("   ", "Morgan")] // whitespace-only
    public async Task PutMe_EmptyOrWhitespaceName_ReturnsFieldError(string first, string last) // AC-433
    {
        var response = await _client.PutAsJsonAsync("/api/Auth/me", new
        {
            firstName = first,
            lastName = last,
            phoneNumber = (string?)null,
            profileImageUrl = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiFailure>();
        body!.Errors.Should().Contain(e => e.Field == "FirstName");
    }

    [Fact]
    public async Task PutMe_NameOverMaxLength_ReturnsFieldError() // AC-433
    {
        var response = await _client.PutAsJsonAsync("/api/Auth/me", new
        {
            firstName = new string('A', 101),
            lastName = "Morgan",
            phoneNumber = (string?)null,
            profileImageUrl = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiFailure>();
        body!.Errors.Should().Contain(e => e.Field == "FirstName");
    }

    [Fact]
    public async Task PutMe_InvalidPhone_ReturnsFieldError() // AC-434
    {
        var response = await _client.PutAsJsonAsync("/api/Auth/me", new
        {
            firstName = "Alex",
            lastName = "Morgan",
            phoneNumber = "12345",
            profileImageUrl = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiFailure>();
        body!.Errors.Should().Contain(e => e.Field == "PhoneNumber");
    }

    [Fact]
    public async Task PutMe_NonHttpsImageUrl_ReturnsFieldError() // AC-435
    {
        var response = await _client.PutAsJsonAsync("/api/Auth/me", new
        {
            firstName = "Alex",
            lastName = "Morgan",
            phoneNumber = (string?)null,
            profileImageUrl = "http://insecure.example.test/avatar.png"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiFailure>();
        body!.Errors.Should().Contain(e => e.Field == "ProfileImageUrl");
    }

    [Fact]
    public async Task PutMe_SamePhone_PreservesConfirmationState() // AC-437
    {
        await SetUserPhoneAsync(_user.Id, "+14155550100", confirmed: true);

        var response = await _client.PutAsJsonAsync("/api/Auth/me", new
        {
            firstName = "Alex",
            lastName = "Morgan",
            phoneNumber = "+14155550100",
            profileImageUrl = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await GetUserPhoneConfirmedAsync(_user.Id);
        confirmed.Should().BeTrue();
    }

    [Fact]
    public async Task PutMe_CannotChangeAnotherUser_ThroughExtraBodyField() // AC-432
    {
        var (otherClient, otherUser) = await _factory.CreateAuthenticatedClientAsync();

        var response = await _client.PutAsJsonAsync("/api/Auth/me", new
        {
            id = otherUser.Id,
            firstName = "Hacked",
            lastName = "ByA",
            phoneNumber = (string?)null,
            profileImageUrl = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var reloaded = await userManager.FindByIdAsync(otherUser.Id.ToString());
        reloaded!.FirstName.Should().NotBe("Hacked");
    }

    [Fact]
    public async Task PutMe_InactiveUser_ReturnsFailure() // AC-438
    {
        await SetUserActiveAsync(_user.Id, false);

        var response = await _client.PutAsJsonAsync("/api/Auth/me", new
        {
            firstName = "Alex",
            lastName = "Morgan",
            phoneNumber = (string?)null,
            profileImageUrl = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- helpers ------------------------------------------------------------------------------

    private async Task SetUserPhoneAsync(Guid userId, string phone, bool confirmed)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        user!.PhoneNumber = phone;
        user.PhoneNumberConfirmed = confirmed;
        await userManager.UpdateAsync(user);
    }

    private async Task<bool> GetUserPhoneConfirmedAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user!.PhoneNumberConfirmed;
    }

    private async Task SetUserActiveAsync(Guid userId, bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (active)
        {
            user!.Activate();
        }
        else
        {
            user!.Deactivate();
        }

        await userManager.UpdateAsync(user);
    }
}
