using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Reports.Dtos;

namespace CustomerSupport.Application.Features.Reports.Queries.GetTicketVolumeReport;

/// <summary>Ticket volume by period/category/priority — AC-149..AC-151.</summary>
public record GetTicketVolumeReportQuery(DateTime From, DateTime To, string GroupBy)
    : IQuery<Response<TicketVolumeReportDto>>;
