using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// ASG-8 — the portal register contract carries an optional phone number and persists it on the
/// created ApplicationUser. Proved against the real register endpoint and the real database,
/// because the contract change (a new request field) is exactly the kind of thing a shape-only
/// test would miss.
/// </summary>
public sealed class PortalRegisterEndpointTests : IClassFixture<CrmApiFactory>
{
    private readonly CrmApiFactory _factory;

    public PortalRegisterEndpointTests(CrmApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> RegisterAsync(string email, string? phoneNumber)
    {
        await _factory.EnsureDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email,
            username = "testuser" + Guid.NewGuid().ToString("N"),
            password = "Password123",
            firstName = "Test",
            lastName = "User",
            phoneNumber,
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Data.Should().NotBe(Guid.Empty);
        return body.Data;
    }

    private async Task<ApplicationUser?> FindUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.FindByEmailAsync(email);
    }

    [Fact]
    public async Task ASG8_Register_PersistsPhoneNumber()
    {
        var email = $"crm-phone-{Guid.NewGuid():N}@test.local";
        const string phone = "  +966 55 123 4567  ";

        var id = await RegisterAsync(email, phone);

        var user = await FindUserAsync(email);
        user.Should().NotBeNull();
        user!.Id.Should().Be(id);
        user.PhoneNumber.Should().Be(phone.Trim());
    }

    [Fact]
    public async Task ASG8_Register_BlankPhone_StaysNull()
    {
        var email = $"crm-nophone-{Guid.NewGuid():N}@test.local";

        await RegisterAsync(email, null);

        var user = await FindUserAsync(email);
        user.Should().NotBeNull();
        user!.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task ASG8_Register_OverLengthPhone_Returns400()
    {
        var client = _factory.CreateClient();
        var email = $"crm-longphone-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email,
            username = "longphoneuser",
            password = "Password123",
            firstName = "Test",
            lastName = "User",
            phoneNumber = new string('5', 21),
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}
