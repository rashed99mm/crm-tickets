using System.Collections.Concurrent;
using CustomerSupport.Shared.Contracts.Messages;

namespace CustomerSupport.Infrastructure.Messaging;

/// <summary>
/// Short-lived, in-process guard that lets a consumer ignore a <see cref="ChatMessagePushed"/> it
/// has already processed (CC-32). MassTransit delivers retried messages at-least-once, so without it
/// a RabbitMQ redelivery would push the same live-chat message to a session twice. The guard is keyed
/// on the persisted <c>MessageId</c>, is bounded so it cannot leak, and is deliberately process-local:
/// each host deduplicates its own pushes, which is all that is needed because each message is only
/// ever published once by its originating host.
/// </summary>
public sealed class ChatMessagePushedDeduplicator
{
    // Each entry keeps the message id and the timestamp it was first seen, so the set can be pruned.
    private sealed record Entry(DateTimeOffset SeenAt);

    private readonly ConcurrentDictionary<Guid, Entry> _seen = new();
    private readonly TimeSpan _window;

    public ChatMessagePushedDeduplicator(TimeSpan? window = null)
    {
        _window = window ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>Returns <c>true</c> if <paramref name="messageId"/> has not been seen within the
    /// retention window and records it as seen; <c>false</c> if it was already processed.</summary>
    public bool TryMark(Guid messageId, DateTimeOffset now)
    {
        Prune(now);

        var fresh = new Entry(now);
        if (_seen.TryAdd(messageId, fresh))
        {
            return true;
        }

        // A concurrent duplicate raced in first; treat as already processed.
        return false;
    }

    public void Clear() => _seen.Clear();

    private void Prune(DateTimeOffset now)
    {
        foreach (var (id, entry) in _seen)
        {
            if (now - entry.SeenAt > _window)
            {
                _seen.TryRemove(id, out _);
            }
        }
    }
}
