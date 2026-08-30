using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Queries.GetPlatformSettings;

public class GetPlatformSettingsQuery(string? category) : BasePagedQuery, IQuery<Response<PaginatedList<PlatformSettingDto>>>
{
    public string? Category { get; init; } = category;
    public bool IncludePrivate { get; init; }
    public string? Key { get; init; }
    
    public GetPlatformSettingsQuery() : this(null)
    {
        PageIndex = 1;
        PageSize = 10;
    }
}
