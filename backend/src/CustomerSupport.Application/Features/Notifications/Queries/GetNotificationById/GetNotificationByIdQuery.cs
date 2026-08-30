using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Notifications.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Notifications.Queries.GetNotificationById;

public record GetNotificationByIdQuery(Guid Id, Guid RequestedByUserId) : IQuery<Response<NotificationDto>>;
