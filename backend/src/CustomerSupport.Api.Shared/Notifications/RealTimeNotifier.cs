using CustomerSupport.Api.Shared.Hubs;
using CustomerSupport.Application.Notifications;
using Microsoft.AspNetCore.SignalR;
using System.Threading;
using System.Threading.Tasks;

namespace CustomerSupport.Api.Shared.Notifications;

/// <summary>
/// Live transport for the in-app channel. Implemented here (not in Infrastructure) because SignalR
/// lives in Api.Shared; Infrastructure depends only on <see cref="IRealTimeNotifier"/>.
/// </summary>
public sealed class RealTimeNotifier : IRealTimeNotifier
{
    private readonly IHubContext<MainHub> _hub;
    private readonly IHubContext<ChatHub> _chatHub;

    public RealTimeNotifier(IHubContext<MainHub> hub, IHubContext<ChatHub> chatHub)
    {
        _hub = hub;
        _chatHub = chatHub;
    }

    public async Task NotifyInAppAsync(Guid userId, InAppPushPayload payload, CancellationToken ct = default)
    {
        var group = NotificationGatewayConstants.UserGroup(userId);
        await _hub.Clients.Group(group).SendAsync(NotificationGatewayConstants.SignalRInAppMethod, payload, ct);
    }

    public async Task NotifyChatMessageAsync(ChatMessagePushPayload payload, CancellationToken ct = default)
    {
        // Staff (authenticated MainHub) receive a broadcast and filter by session on the client.
        await _hub.Clients.All.SendAsync(NotificationGatewayConstants.SignalRChatMessageMethod, payload, ct);

        // The customer on the anonymous /hubs/chat is joined to chat:{sessionId} on connect, so the
        // push reaches only the one session's connection — never another customer's conversation.
        await _chatHub.Clients
            .Group(NotificationGatewayConstants.ChatSessionGroup(payload.SessionId))
            .SendAsync(NotificationGatewayConstants.SignalRChatMessageMethod, payload, ct);
    }
}
