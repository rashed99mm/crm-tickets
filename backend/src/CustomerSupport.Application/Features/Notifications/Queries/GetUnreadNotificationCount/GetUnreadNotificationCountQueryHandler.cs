using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

public class GetUnreadNotificationCountQueryHandler(
    IRepository<Notification> notificationRepository,
    IMessageFactory messages)
    : IQueryHandler<GetUnreadNotificationCountQuery, Response<int>>
{
    public async Task<Response<int>> Handle(GetUnreadNotificationCountQuery request, CancellationToken ct)
    {
        var unreadCount = await notificationRepository.CountAsync(
            n => n.UserId == request.UserId && n.ReadAt == null,
            ct);

        return messages.Success(unreadCount, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
