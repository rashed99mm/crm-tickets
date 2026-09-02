namespace CustomerSupport.Application.Channels;

/// <summary>
/// CC-32 — mocks must never be active in production. A mock gateway that accepts and discards
/// customer notifications is worse than an outage, because every send reports success and nothing
/// alerts. A pure function so the rule is unit-tested without booting a host.
/// </summary>
public static class ChannelMockGuard
{
    public static (bool IsLegal, string? Error) Validate(bool useMocks, string? environmentName)
    {
        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

        return useMocks && isProduction
            ? (false, "Channels:UseMocks must not be true when the environment is Production. "
                    + "Remove the setting or point the channel gateways at real providers.")
            : (true, null);
    }
}
