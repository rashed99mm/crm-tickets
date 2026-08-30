using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Queries.GetBranding;

public record GetBrandingQuery : IQuery<Response<BrandingDto>>;
