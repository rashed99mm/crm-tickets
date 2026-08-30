using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Channels;
using CustomerSupport.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CustomerSupport.Api.Shared.Hubs;

/// <summary>
/// Anonymous live-chat hub at <c>/hubs/chat</c>, separate from the authenticated <c>/hubs/main</c>.
///
/// A customer authenticates to this hub not with a JWT but with the opaque session token returned
/// by <c>POST /api/external/chat/start</c>, passed in the SignalR query string (<c>?token=</c>) — the
/// same token the REST send/transcript endpoints take. On connect the hub hashes the token and
/// looks the session up by its stored <see cref="LiveChatSession.SessionTokenHash"/>, then subscribes
/// the connection to the per-session group <c>chat:{sessionId}</c> so <see cref="RealTimeNotifier"/>
/// delivers only this session's messages. An unknown, malformed, or closed session is aborted.
///
/// The connection is deliberately session-scoped: the client can never choose another session id,
/// because the group membership derives from the token it presented.
/// </summary>
public sealed class ChatHub : Hub
{
    private readonly IRepository<LiveChatSession> _sessions;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IRepository<LiveChatSession> sessions, ILogger<ChatHub> logger)
    {
        _sessions = sessions;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Query["token"].ToString();

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Live-chat connection rejected: no session token");
            Context.Abort();
            return;
        }

        string hash;
        try
        {
            hash = LiveChatSession.HashToken(token);
        }
        catch (ArgumentException)
        {
            _logger.LogWarning("Live-chat connection rejected: malformed session token");
            Context.Abort();
            return;
        }

        var session = await _sessions.FirstOrDefaultAsync(s => s.SessionTokenHash == hash, CancellationToken.None);
        if (session is null)
        {
            _logger.LogWarning("Live-chat connection rejected: unknown session token");
            Context.Abort();
            return;
        }

        try
        {
            session.EnsureOpenForCustomer();
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Live-chat connection rejected for session {SessionId}: not open", session.Id);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGatewayConstants.ChatSessionGroup(session.Id));
        _logger.LogInformation("Live-chat client {ConnectionId} joined session {SessionId}", Context.ConnectionId, session.Id);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Group membership is removed automatically when the connection drops; nothing to clean up.
        await base.OnDisconnectedAsync(exception);
    }
}
