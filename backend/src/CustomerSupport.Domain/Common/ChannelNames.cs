namespace CustomerSupport.Domain.Common;

/// <summary>
/// The permitted channel names, in one place (CC-48). Four divergent copies existed before this:
/// the entity's own array, two command validators, and CreateTicket's Ticket.Source set — and they
/// disagreed, with `Portal` missing from one and `Email` absent from the inbound list entirely.
/// Every name must fit TicketMessage.Channel's nvarchar(20).
/// </summary>
public static class ChannelNames
{
    public const string Email = "Email";
    public const string System = "System";
    public const string WhatsApp = "WhatsApp";
    public const string Sms = "SMS";
    public const string WebForm = "WebForm";
    public const string LiveChat = "LiveChat";
    public const string Portal = "Portal";

    /// <summary>Every value TicketMessage.Channel may hold.</summary>
    public static readonly string[] All = [Email, System, WhatsApp, Sms, WebForm, LiveChat, Portal];

    /// <summary>
    /// Channels an inbound customer message can arrive on. `System` is machine-authored and
    /// `Portal` has its own authenticated command, so neither is ingestible here.
    /// </summary>
    public static readonly string[] Inbound = [Email, WhatsApp, Sms, WebForm, LiveChat];

    /// <summary>Values Ticket.Source may hold — where a ticket originated.</summary>
    public static readonly string[] TicketSources = [Portal, WebForm, WhatsApp, Sms, Email, LiveChat];

    public static bool IsKnown(string? channel) =>
        channel is not null && Array.Exists(All, c => string.Equals(c, channel, StringComparison.Ordinal));
}
