using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Integrations.Commands.ImportCmsErpTickets;

public sealed record ImportCmsErpTicketsCommand : ICommand<Response<ImportCmsErpTicketsResult>>;

public sealed record ImportCmsErpTicketsResult(
    int Imported,
    int Skipped,
    IReadOnlyList<string> TicketReferences);
