namespace CustomerSupport.Application.Features.Tickets.Dtos;

/// <summary>One entry of a ticket's message timeline (AC-106). SenderName is resolved at read
/// time from SenderId, the same arrangement TicketHistory's actor names and CustomerNote's author
/// names use — the row stores no name.</summary>
public record TicketMessageDto(
    Guid Id, string Direction, string Channel, string? Subject, string Body,
    Guid SenderId, string SenderName, DateTime SentAt);
