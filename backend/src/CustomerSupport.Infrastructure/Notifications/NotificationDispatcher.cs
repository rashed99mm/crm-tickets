using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomerSupport.Infrastructure.Notifications;

/// <summary>
/// Resolves a <see cref="INotificationChannelSender"/> for a <see cref="NotificationChannel"/> from
/// the registered senders. This is the only routing seam the gateway uses.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IReadOnlyCollection<INotificationChannelSender> _senders;

    public NotificationDispatcher(IEnumerable<INotificationChannelSender> senders)
    {
        _senders = senders.ToList();
    }

    public IReadOnlyCollection<INotificationChannelSender> Senders => _senders;

    public INotificationChannelSender GetSender(NotificationChannel channel) =>
        _senders.FirstOrDefault(s => s.SupportedChannel == channel)
        ?? throw new InvalidOperationException($"No sender registered for channel {channel.Value}");
}
