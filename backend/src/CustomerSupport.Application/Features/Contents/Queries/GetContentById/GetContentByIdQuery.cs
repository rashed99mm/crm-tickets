using CustomerSupport.Application.Contracts;

using CustomerSupport.Application.Features.Contents.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Contents.Queries.GetContentById;

public record GetContentByIdQuery(Guid Id) : IQuery<Response<ContentDto>>;
