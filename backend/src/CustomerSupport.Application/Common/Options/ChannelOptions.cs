namespace CustomerSupport.Application.Common.Options;

/// <summary>
/// The `Channels:*` settings. `UseMocks` swaps the three channel gateways over to
/// cms-integration-gateway's provider-faithful mocks (CC-30/CC-31); everything else stays on the
/// database configuration. Lives in Application so both hosts and the Infrastructure decorator can
/// bind it without Application referencing Infrastructure.
/// </summary>
public sealed class ChannelOptions
{
    public const string SectionName = "Channels";

    public bool UseMocks { get; set; }

    public string MockBaseUrl { get; set; } = "http://localhost:3001";

    /// <summary>Shared with the mock so its outbound webhooks carry a signature we can verify.</summary>
    public string MockWebhookSecret { get; set; } = string.Empty;

    /// <summary>SendGrid requires a `from`; the old house payload had none.</summary>
    public string EmailFrom { get; set; } = "no-reply@commandcenter.local";

    /// <summary>Twilio requires a `From`.</summary>
    public string SmsFrom { get; set; } = "CommandCenter";
}
