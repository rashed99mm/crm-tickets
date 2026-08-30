namespace CustomerSupport.Shared.Contracts.Messages;

/// <summary>
/// Published once after a live-chat message is persisted, whenever either party sends one. Carries
/// everything a consumer needs to push the message in real time to the recipient without another
/// lookup: the session group it belongs to, the sender, and the body. Emission is governed by the
/// <c>IMessagePublisher</c> port; the <c>ChatMessagePushedConsumer</c> runs on each host and pushes to
/// that host's locally-owned SignalR hub, which is what delivers across the InternalApi/ExternalApi
/// process boundary (CC-30/CC-31/CC-34).
/// </summary>
public sealed record ChatMessagePushed(
    Guid MessageId,
    Guid SessionId,
    string SenderType,
    string SenderName,
    Guid? SenderId,
    string Body,
    DateTime SentAt);
