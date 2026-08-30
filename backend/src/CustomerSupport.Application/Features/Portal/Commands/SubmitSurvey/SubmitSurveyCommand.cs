using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Portal.Commands.SubmitSurvey;

/// <summary>A customer submits a satisfaction survey for a resolved ticket they own (US-408/US-409, PJ-11/12).</summary>
public record SubmitSurveyCommand(
    Guid TicketId,
    int Rating,
    string? Comment,
    Guid CustomerId) : ICommand<Response<Guid>>;