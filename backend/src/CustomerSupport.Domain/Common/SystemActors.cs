namespace CustomerSupport.Domain.Common;

/// <summary>
/// Well-known identities that are not real users. <see cref="Entities.Tickets.TicketHistory.Record"/>
/// and <see cref="Entities.Tickets.TicketMessage.Create"/> refuse <c>Guid.Empty</c>, so any
/// system-attributed row needs a stable, non-empty, non-real-user id.
///
/// Lives in Domain (not Infrastructure) so <c>Application</c> can reference it without crossing the
/// dependency rule — previously here, it sat in <c>Infrastructure.Sla</c> and the channel-ingestion
/// handlers (which are Application features) had nowhere legal to reach it from.
/// </summary>
public static class SystemActors
{
    /// <summary>The actor recorded against an auto-escalation <c>Escalated</c> history row.</summary>
    public static readonly Guid EscalationEngine = new("E0000000-0000-0000-0000-000000000001");

    /// <summary>
    /// The actor recorded as a <see cref="Entities.Tickets.TicketMessage.SenderId"/> for a message
    /// ingested from an external channel (WhatsApp, SMS, web form, live-chat customer) with no agent
    /// involved. Not the customer — <c>TicketMessage.SenderId</c> never holds a customer identity
    /// (conversation-record spec A1) — this is the channel itself acting.
    /// </summary>
    public static readonly Guid ChannelIngestion = new("E0000000-0000-0000-0000-000000000002");
}