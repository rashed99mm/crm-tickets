using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

public record GetUnreadNotificationCountQuery(Guid UserId) : IQuery<Response<int>>;
