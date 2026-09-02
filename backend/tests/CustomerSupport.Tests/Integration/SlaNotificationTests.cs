using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Infrastructure.Jobs;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// US-219 — a recorded SLA breach notifies the assignee through the in-app channel. Real LocalDB,
/// one scanner pass per test, asserted on the durable Notification row the InApp sender writes.
/// </summary>
public class SlaNotificationTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private Guid _categoryId;
    private Guid _customerId;
    private Guid _adminUserId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        _categoryId = await _factory.EnsureCategoryAsync("SlaNotificationTech");

        var customer = await _admin.PostAsJsonAsync("/api/Customers", new
        {
            name = "US219 Customer",
            email = $"us219-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });
        customer.StatusCode.Should().Be(HttpStatusCode.Created);
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _adminUserId = await db.Users
            .Where(u => u.Email == "admin@cce-platform.com")
            .Select(u => u.Id)
            .FirstAsync();
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateTicketAsync()
    {
        var response = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "US-219 notification test ticket",
            description = "A breach should page the assignee.",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "High",
            urgency = "Medium",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    /// <summary>Sets the assignee directly — assignment's domain guards belong to other tests.</summary>
    private async Task AssignInDbAsync(Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
        db.Entry(ticket).Property(t => t.AssigneeId).CurrentValue = _adminUserId;
        await db.SaveChangesAsync();
    }

    private static async Task BackdateResponseDueAsync(Guid ticketId, IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
        db.Entry(ticket).Property(t => t.ResponseDueAt).CurrentValue = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
    }

    private async Task BackdateResponseDueAsync(Guid ticketId)
        => await BackdateResponseDueAsync(ticketId, _factory.Services);

    private async Task<int> ScanAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>().ScanAsync();
    }

    private async Task<List<Notification>> NotificationsForAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // The shared LocalDB keeps rows across tests and runs, so every assertion scopes to THIS
        // test's ticket by its unique reference inside the rendered message. The customer is
        // unique per test, so the reference resolves unambiguously.
        var reference = await db.Tickets
            .Where(t => t.CustomerId == _customerId)
            .Select(t => t.Reference)
            .FirstAsync();

        return await db.Set<Notification>().AsNoTracking()
            .Where(n => n.UserId == userId && n.Title == "SLA breached" && n.Message.Contains(reference))
            .ToListAsync();
    }

    [Fact]
    [Trait("AC", "219.1")]
    public async Task AC219_BreachNotifiesAssignee()
    {
        var ticketId = await CreateTicketAsync();
        await AssignInDbAsync(ticketId);
        await BackdateResponseDueAsync(ticketId);

        (await ScanAsync()).Should().BeGreaterThan(0);

        var notifications = await NotificationsForAsync(_adminUserId);
        notifications.Should().ContainSingle("one breach pass, one in-app notification");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reference = await db.Tickets.Where(t => t.Id == ticketId).Select(t => t.Reference).FirstAsync();
        notifications.Single().Message.Should().Contain(reference);
    }

    [Fact]
    [Trait("AC", "219.2")]
    public async Task AC219_RepeatedPassWithoutNewBreach_DoesNotDuplicateNotification()
    {
        var ticketId = await CreateTicketAsync();
        await AssignInDbAsync(ticketId);
        await BackdateResponseDueAsync(ticketId);

        (await ScanAsync()).Should().BeGreaterThan(0);
        (await ScanAsync()).Should().Be(0, "AC-132 records the breach once; a repeat pass must not re-notify");

        (await NotificationsForAsync(_adminUserId)).Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "219.3")]
    public async Task AC219_UnassignedTicket_DoesNotNotify()
    {
        var ticketId = await CreateTicketAsync();
        await BackdateResponseDueAsync(ticketId);

        (await ScanAsync()).Should().BeGreaterThan(0);

        var found = await NotificationsForAsync(_adminUserId);
        found.Should().BeEmpty("found: {0}", string.Join(" | ", found.Select(n => n.Message)));
    }
}
