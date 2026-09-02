using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;

namespace CustomerSupport.Infrastructure.ExternalApis.Providers;

/// <summary>
/// CC-30/CC-31/CC-33 — the whole mock/real toggle. Every channel sender and the inbound signature
/// verifier read their base URL and credential through IExternalApiConfigurationProvider and
/// nothing else, so decorating that one port is enough: no sender, handler or controller learns
/// that mocks exist.
/// </summary>
public sealed class MockRoutingExternalApiConfigurationProvider : IExternalApiConfigurationProvider
{
    /// <summary>
    /// Provider-faithful paths, so flipping the flag changes only the host. The account sid and
    /// phone-number id are fixed dev values the mock also hard-codes; in real mode both arrive
    /// inside the configured BaseUrl instead.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> MockPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NotificationGatewayConstants.EmailGatewayConfigName] = "mock/sendgrid/v3/mail/send",
            [NotificationGatewayConstants.SmsGatewayConfigName] = "mock/twilio/2010-04-01/Accounts/ACmockaccountsid/Messages.json",
            [NotificationGatewayConstants.WhatsAppGatewayConfigName] = "mock/meta/v18.0/100000000000000/messages",
        };

    private readonly IExternalApiConfigurationProvider _inner;
    private readonly ChannelOptions _options;

    public MockRoutingExternalApiConfigurationProvider(
        IExternalApiConfigurationProvider inner,
        ChannelOptions options)
    {
        _inner = inner;
        _options = options;
    }

    public ExternalApiConfig? GetConfig(string apiName) =>
        _options.UseMocks && MockPaths.TryGetValue(apiName, out var path)
            ? MockConfig(path)
            : _inner.GetConfig(apiName);

    public IReadOnlyList<ExternalApiConfig> GetAllConfigs() => _inner.GetAllConfigs();

    public Task ReloadAsync(CancellationToken ct = default) => _inner.ReloadAsync(ct);

    private ExternalApiConfig MockConfig(string path) => new()
    {
        BaseUrl = $"{_options.MockBaseUrl.TrimEnd('/')}/{path}",
        TimeoutSeconds = 30,
        Auth = new ExternalApiAuthConfig
        {
            // None: the mock needs no credential, and this keeps ApplyAuth from building a header
            // out of a value nobody set. Value still carries the shared secret because the inbound
            // signature verifier reads Auth.Value irrespective of Type.
            Type = ExternalApiAuthType.None,
            Value = _options.MockWebhookSecret,
        },
    };
}
