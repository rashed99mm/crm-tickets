using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Notifications.Commands.DeleteNotification;

public record DeleteNotificationCommand(Guid Id) : ICommand<Response<Unit>>;
