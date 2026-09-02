using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-15 — creating a ticket publishes a domain event that notifies **both** the ticket's customer
/// (when that customer has a linked portal login) and the acting staff creator (AC-N7), through the
/// in-app channel. Real LocalDB, end-to-end through the API. AC-N2 asserts the durable
/// <c>Notification</c> rows for both recipients; AC-N5 asserts the no-linked-user case still notifies
/// the creator and returns 201. (AC-N3 — the live SignalR push — is verified with a real
/// <c>@microsoft/signalr</c> Node client against the running Internal API; see the evidence in the
/// feature commit and the plan's deviation log.)
/// </summary>
public class TicketCreatedNotificationTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private ApplicationUser _adminUser = null!;
    private Guid _categoryId;
    private Guid _creatorId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _adminUser) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        _creatorId = _adminUser.Id;
        _categoryId = await _factory.EnsureCategoryAsync("TicketCreatedTech");
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateCustomerAsync()
    {
        var response = await _admin.PostAsJsonAsync("/api/Customers", new
        {
            name = "N-Notified Customer",
            email = $"n-created-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    /// <summary>Creates a portal login and links it to the customer — exactly what US-401 does.</summary>
    private async Task<Guid> CreateLinkedPortalUserAsync(Guid customerId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"portal-{Guid.NewGuid():N}@test.local";
        var user = ApplicationUser.Create(email, email, "Portal", "User");
        user.LinkCustomer(customerId);
        var result = await userManager.CreateAsync(user, "Test-Password-456");
        result.Succeeded.Should().BeTrue(string.Join(", ", result.Errors.Select(e => e.Description)));
        return user.Id;
    }

    private async Task<Guid> CreateTicketAsync(Guid customerId)
    {
        var response = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Ticket-created in-app notification",
            description = "A staff-created ticket should notify the linked customer.",
            customerId,
            categoryId = _categoryId,
            impact = "Medium",
            urgency = "Medium",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    [Fact]
    [Trait("AC", "N2")]
    public async Task AC_N2_StaffCreateTicket_WritesDurableInAppNotificationForLinkedCustomerAndCreator()
    {
        var customerId = await CreateCustomerAsync();
        var portalUserId = await CreateLinkedPortalUserAsync(customerId);
        var ticketId = await CreateTicketAsync(customerId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reference = await db.Tickets.Where(t => t.Id == ticketId).Select(t => t.Reference).FirstAsync();

        // Dispatch is synchronous inside the create request (UnitOfWork publishes right after the
        // committed save), so both durable rows must already be committed when the 201 is returned.
        var notifications = await db.Set<Notification>().AsNoTracking()
            .Where(n => n.NotificationType == "TICKET_CREATED" && n.Message.Contains(reference))
            .ToListAsync();

        // One row per recipient: the linked customer and the acting staff creator (AC-N1/N7).
        notifications.Select(n => n.UserId).Should().BeEquivalentTo([portalUserId, _creatorId]);

        foreach (var notification in notifications)
        {
            notification.Channel.Should().Be("InApp");
            notification.Status.Should().Be("Sent");
            notification.Title.Should().Be("Ticket created");
            notification.Message.Should().Contain(reference);
        }
    }

    [Fact]
    [Trait("AC", "N5")]
    public async Task AC_N5_StaffCreateTicket_WithNoLinkedPortalUser_NotifiesCreatorButNotCustomer()
    {
        var customerId = await CreateCustomerAsync(); // no portal user linked
        var ticketId = await CreateTicketAsync(customerId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reference = await db.Tickets.Where(t => t.Id == ticketId).Select(t => t.Reference).FirstAsync();

        var rows = await db.Set<Notification>().AsNoTracking()
            .Where(n => n.Message.Contains(reference))
            .ToListAsync();

        // AC-N5: a customer without a linked portal user receives nothing — but AC-N7 means the
        // creator still does, so exactly one row, to the creator, not the customer.
        rows.Should().ContainSingle();
        rows.Single().UserId.Should().Be(_creatorId);
        rows.Single().NotificationType.Should().Be("TICKET_CREATED");
    }
}
