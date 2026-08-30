using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Services;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Notifications;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Notifications;

public class NotificationGatewayTests
{
    private sealed class FakeRenderer : INotificationTemplateRenderer
    {
        public Task<RenderedNotification> RenderAsync(NotificationDispatchRequest request, NotificationChannel channel, CancellationToken ct = default) =>
            Task.FromResult(new RenderedNotification(
                request.RecipientUserId, request.Email, request.PhoneNumber,
                request.TemplateCode, request.TemplateCode, request.TemplateCode, channel, null));
    }

    private sealed class FakeSender : INotificationChannelSender
    {
        public FakeSender(NotificationChannel channel) => SupportedChannel = channel;
        public NotificationChannel SupportedChannel { get; }
        public Task<ChannelSendResult> SendAsync(RenderedNotification notification, CancellationToken ct = default) =>
            Task.FromResult(new ChannelSendResult(SupportedChannel, true));
    }

    [Fact]
    public void Dispatcher_Resolves_Registered_Sender_And_Throws_For_Unknown()
    {
        var inApp = new FakeSender(NotificationChannel.InApp);
        var dispatcher = new NotificationDispatcher(new List<INotificationChannelSender> { inApp });

        dispatcher.GetSender(NotificationChannel.InApp).Should().BeSameAs(inApp);
        var act = () => dispatcher.GetSender(NotificationChannel.Email);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Gateway_Returns_Failed_For_Unsupported_Channel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var dispatcher = new NotificationDispatcher(new List<INotificationChannelSender>());
        var gateway = new NotificationGateway(dispatcher, new FakeRenderer(), db, NullLogger<NotificationGateway>.Instance);

        var result = await gateway.SendAsync(new NotificationDispatchRequest(
            "TEST", Guid.NewGuid(), new[] { NotificationChannel.Email },
            new Dictionary<string, string>(), null, null, false, null, null));

        result.Succeeded.Should().BeFalse();
        result.ChannelResults.Should().ContainSingle(r =>
            r.Channel == NotificationChannel.Email && !r.Succeeded &&
            r.ErrorCode == ApplicationErrors.Notification.CHANNEL_NOT_SUPPORTED);
    }

    [Fact]
    public async Task InApp_Sender_Persists_Notification_And_Pushes_SignalR()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var notifier = new Mock<IRealTimeNotifier>();
        var sender = new InAppNotificationChannelSender(
            new NotificationDomainService(), db, notifier.Object, NullLogger<InAppNotificationChannelSender>.Instance);

        var userId = Guid.NewGuid();
        var result = await sender.SendAsync(new RenderedNotification(
            userId, null, null, "Title", "Body", "TEST", NotificationChannel.InApp, null));

        result.Succeeded.Should().BeTrue();
        var stored = await db.Set<Notification>().SingleAsync();
        stored.Channel.Should().Be("InApp");
        stored.Status.Should().Be("Sent");
        stored.UserId.Should().Be(userId);
        notifier.Verify(n => n.NotifyInAppAsync(userId, It.IsAny<InAppPushPayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InApp_Sender_Without_Recipient_Is_Safe_Failure()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var notifier = new Mock<IRealTimeNotifier>();
        var sender = new InAppNotificationChannelSender(
            new NotificationDomainService(), db, notifier.Object, NullLogger<InAppNotificationChannelSender>.Instance);

        var result = await sender.SendAsync(new RenderedNotification(
            null, null, null, "Title", "Body", "TEST", NotificationChannel.InApp, null));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrors.Notification.INAPP_REQUIRES_USER);
        notifier.Verify(n => n.NotifyInAppAsync(It.IsAny<Guid>(), It.IsAny<InAppPushPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        (await db.Set<Notification>().CountAsync()).Should().Be(0);
    }
}
