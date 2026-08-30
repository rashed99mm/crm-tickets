using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Notifications.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Notifications.Queries.GetNotifications;

public class GetNotificationsQueryHandler(IRepository<Notification> notificationRepository)
    : IQueryHandler<GetNotificationsQuery, Response<PaginatedList<NotificationDto>>>
{
    public async Task<Response<PaginatedList<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var filter = PredicateBuilder.True<Notification>()
            .WhereIf(request.UserId.HasValue, n => n.UserId == request.UserId!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Status), n => n.Status == request.Status)
            .WhereIf(!string.IsNullOrWhiteSpace(request.NotificationType), n => n.NotificationType == request.NotificationType)
            .WhereIf(request.IsRead.HasValue, n => (request.IsRead!.Value ? n.ReadAt != null : n.ReadAt == null));

        var result = await notificationRepository.GetPagedAsync<NotificationDto>(request, filter, ct);
        return Response<PaginatedList<NotificationDto>>.Ok(result, SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
