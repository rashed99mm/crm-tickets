using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Reports.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Reports.Queries.GetCsatReport;

public record GetCsatReportQuery(DateTime From, DateTime To) : IQuery<Response<CsatReportDto>>;
