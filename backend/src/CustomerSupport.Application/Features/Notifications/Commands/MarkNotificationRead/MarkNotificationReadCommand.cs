using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Notifications.Commands.MarkNotificationRead;

public record MarkNotificationReadCommand(Guid Id) : ICommand<Response<Unit>>;
