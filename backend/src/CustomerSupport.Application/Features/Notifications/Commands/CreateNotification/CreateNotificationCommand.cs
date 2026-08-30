using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Notifications.Commands.CreateNotification;

public record CreateNotificationCommand(
    Guid UserId,
    string Title,
    string Message,
    string NotificationType,
    string Channel,
    string? Metadata
) : ICommand<Response<Guid>>;
