using CustomerSupport.Domain.Entities.Notifications;

namespace CustomerSupport.Domain.Specifications.Notifications;

public class GetNotificationByIdSpec : BaseSpecification<Domain.Entities.Notifications.Notification>
{
    public GetNotificationByIdSpec(Guid notificationId)
    {
        SetCriteria(n => n.Id == notificationId);
    }
}

public class GetUnreadNotificationsSpec : BaseSpecification<Domain.Entities.Notifications.Notification>
{
    public GetUnreadNotificationsSpec(Guid userId)
    {
        SetCriteria(n => n.UserId == userId && n.ReadAt == null && n.Status != "Cancelled" && !n.IsDeleted);
        ApplyOrderByDescending(n => n.CreatedAt);
    }
}

public class GetNotificationsByUserSpec : BaseSpecification<Domain.Entities.Notifications.Notification>
{
    public GetNotificationsByUserSpec(Guid userId)
    {
        SetCriteria(n => n.UserId == userId && !n.IsDeleted);
        ApplyOrderByDescending(n => n.CreatedAt);
    }
}
