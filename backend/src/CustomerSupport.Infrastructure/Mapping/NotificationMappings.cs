using AutoMapper;
using CustomerSupport.Application.Features.Notifications.Dtos;
using CustomerSupport.Domain.Entities.Notifications;

namespace CustomerSupport.Infrastructure.Mapping;

public class NotificationMappings : Profile
{
    public NotificationMappings()
    {
        CreateMap<Notification, NotificationDto>();
    }
}
