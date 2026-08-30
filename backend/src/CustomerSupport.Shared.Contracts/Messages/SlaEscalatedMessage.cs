namespace CustomerSupport.Shared.Contracts.Messages;

/// <summary>
/// Published once each time a ticket's escalation level advances (US-218, AC-218.1/AC-218.2).
/// Carries the previous and next level plus the level's breach threshold and target role so a
/// consumer can route or notify without another lookup. Emission on the shared bus is governed by
/// the <c>IMessagePublisher</c> port; no consumer is wired this pass (spec addendum A13).
/// </summary>
public record SlaEscalatedMessage(
    Guid TicketId,
    string Reference,
    string PreviousLevel,
    string NextLevel,
    string? TargetRole,
    int BreachMinutes,
    DateTime BreachedAt);
