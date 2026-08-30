using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.RecordTicketMessage;

/// <summary>Records a message against a ticket — AC-101.</summary>
public record RecordTicketMessageCommand(Guid TicketId, string Direction, string Channel, string? Subject, string Body)
    : ICommand<Response<Guid>>;

/// <summary>The record-message payload. No SenderId — the handler takes it from the session (spec A1).</summary>
public record RecordTicketMessageRequest(string Direction, string Channel, string? Subject, string Body);
