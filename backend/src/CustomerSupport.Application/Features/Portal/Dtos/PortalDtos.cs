namespace CustomerSupport.Application.Features.Portal.Dtos;

/// <summary>A ticket as a customer sees it in their own list (US-405, PJ-8).</summary>
public record PortalTicketListItemDto(
    Guid Id,
    string Reference,
    string Subject,
    string Status,
    DateTime CreatedAt);

/// <summary>One message in a ticket timeline (US-413, PJ-15).</summary>
public record PortalMessageDto(
    string Direction,
    string Body,
    DateTime SentAt);

/// <summary>A ticket's full detail for the customer (US-406, PJ-9/15/16).</summary>
public record PortalTicketDetailDto(
    Guid Id,
    string Reference,
    string Subject,
    string Description,
    string Status,
    string Priority,
    DateTime CreatedAt,
    IReadOnlyList<PortalMessageDto> Messages,
    bool SurveySubmitted);