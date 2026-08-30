using AutoMapper;
using CustomerSupport.Application.Features.Contents.Dtos;
using CustomerSupport.Domain.Entities.Content;

namespace CustomerSupport.Infrastructure.Mapping;

public class ContentMappings : Profile
{
    public ContentMappings()
    {
        CreateMap<Content, ContentDto>();
    }
}
