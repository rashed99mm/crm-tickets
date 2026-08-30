using Microsoft.AspNetCore.SignalR;

namespace CustomerSupport.Api.Shared.Hubs;

public class MainHub : Hub
{
    private readonly ILogger<MainHub> _logger;

    public MainHub(ILogger<MainHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        // FEAT-15 / NG-10 — subscribe the connection to its own user group so in-app pushes
        // (RealTimeNotifier -> user:{userId}) reach only the owning user. The client never
        // calls JoinGroup, so it cannot subscribe to another user's group.
        if (!string.IsNullOrEmpty(Context.UserIdentifier))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{Context.UserIdentifier}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        if (!string.IsNullOrEmpty(Context.UserIdentifier))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{Context.UserIdentifier}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
    }
}
