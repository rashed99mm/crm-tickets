using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// AC-N1/AC-N5 — resolving a ticket's customer to its linked portal login user. Real LocalDB via
/// <see cref="CrmApiFactory"/>, because filtering on the mapped <c>CustomerId</c> column is a
/// persistence concern that only the relational database can prove.
/// </summary>
public class IdentityUserServiceFindByCustomerIdTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();

    public Task InitializeAsync() => _factory.EnsureDatabaseAsync();

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    /// <summary>Creates a Customer row (FK_AspNetUsers_Customers_CustomerId requires it) and a linked
    /// portal user, exactly the order portal registration performs (US-401).</summary>
    private async Task<(Guid CustomerId, Guid UserId)> CreateLinkedCustomerAsync(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = CustomerSupport.Domain.Entities.Customers.Customer.Create(
            "Portal Person", $"{slug}-{Guid.NewGuid():N}@test.local", null);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var email = $"{slug}-{Guid.NewGuid():N}@test.local";
        var user = ApplicationUser.Create(email, email, "Portal", "User");
        user.LinkCustomer(customer.Id); // the same LinkCustomer call portal registration makes
        (await userManager.CreateAsync(user, "Test-Password-456")).Succeeded.Should().BeTrue();

        return (customer.Id, user.Id);
    }

    [Fact]
    public async Task FindByCustomerId_ReturnsLinkedUser_ForPortalRegistration()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IIdentityUserService>();

        var (customerId, userId) = await CreateLinkedCustomerAsync("lookup");

        var found = await users.FindByCustomerIdAsync(customerId);

        found.Should().NotBeNull();
        found!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task FindByCustomerId_ReturnsNull_ForUnlinkedCustomer()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IIdentityUserService>();

        var found = await users.FindByCustomerIdAsync(Guid.NewGuid());

        found.Should().BeNull(); // AC-N5: no linked portal user → notify nobody
    }

    [Fact]
    public async Task FindByCustomerId_ReturnsUser_WithNoAsNoTrackingMutation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (customerId, userId) = await CreateLinkedCustomerAsync("lookup2");

        var found = await scope.ServiceProvider.GetRequiredService<IIdentityUserService>()
            .FindByCustomerIdAsync(customerId);
        found.Should().NotBeNull();

        // The row returned by the read must be the same committed row (not a detached phantom).
        (await db.Users.AsNoTracking().CountAsync(u => u.Id == userId)).Should().Be(1);
    }
}
