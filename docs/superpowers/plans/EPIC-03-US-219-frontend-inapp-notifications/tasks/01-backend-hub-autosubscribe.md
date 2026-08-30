# Task 0 — Backend: hub auto-subscribe (security)

**Satisfies:** FN-1, FN-6  
**Files:** `backend/src/CustomerSupport.Api.Shared/Hubs/MainHub.cs`

## Steps

1. In `OnConnectedAsync`, resolve the authenticated user identifier and subscribe the connection to its
   own group (matches `NotificationGatewayConstants.UserGroup` used by `RealTimeNotifier`):

   ```csharp
   public override async Task OnConnectedAsync()
   {
       _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
       if (!string.IsNullOrEmpty(Context.UserIdentifier))
           await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{Context.UserIdentifier}");
       await base.OnConnectedAsync();
   }
   ```

2. In `OnDisconnectedAsync`, remove the connection from that group:

   ```csharp
   public override async Task OnDisconnectedAsync(Exception? exception)
   {
       _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
       if (!string.IsNullOrEmpty(Context.UserIdentifier))
           await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{Context.UserIdentifier}");
       await base.OnDisconnectedAsync(exception);
   }
   ```

3. The client no longer calls `JoinGroup` for in-app delivery; the `JoinGroup`/`LeaveGroup` methods stay
   but are unused by the in-app flow (server enforces group membership).

## Run
`dotnet build backend/CustomerSupport.slnx`

## Expected
Build clean. An in-app push via `RealTimeNotifier` reaches only the owning user's connection.
