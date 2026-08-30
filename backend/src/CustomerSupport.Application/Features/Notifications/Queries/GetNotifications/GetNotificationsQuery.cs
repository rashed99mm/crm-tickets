using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Notifications.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Notifications.Queries.GetNotifications;

public class GetNotificationsQuery(Guid? userId) : BasePagedQuery, IQuery<Response<PaginatedList<NotificationDto>>>
{
    public Guid? UserId { get; init; } = userId;
    public string? Status { get; init; }
    public string? NotificationType { get; init; }
    public bool? IsRead { get; init; }
    
    public GetNotificationsQuery() : this(null)
    {
        PageIndex = 1;
        PageSize = 10;
    }
}
