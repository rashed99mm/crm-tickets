using CustomerSupport.Domain.Entities;

namespace CustomerSupport.Domain.Entities.ExternalApis;

public class ExternalApiConfiguration : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public int TimeoutSeconds { get; private set; } = 30;
    public bool IsEnabled { get; private set; } = true;

    public string AuthType { get; private set; } = "None";
    public string? AuthKeyName { get; private set; }
    public string? AuthKeyLocation { get; private set; }
    public string? AuthValue { get; private set; }
    public string? AuthToken { get; private set; }
    public string? AuthTokenUrl { get; private set; }
    public string? AuthClientId { get; private set; }
    public string? AuthClientSecret { get; private set; }
    public string? AuthScope { get; private set; }
    public bool AuthAutoRefresh { get; private set; }

    public static ExternalApiConfiguration Create(
        string name,
        string baseUrl,
        int timeoutSeconds,
        string authType,
        string? authKeyName = null,
        string? authKeyLocation = null,
        string? authValue = null,
        string? authToken = null,
        string? authTokenUrl = null,
        string? authClientId = null,
        string? authClientSecret = null,
        string? authScope = null,
        bool authAutoRefresh = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required", nameof(baseUrl));
        if (timeoutSeconds <= 0)
            throw new ArgumentException("Timeout must be positive", nameof(timeoutSeconds));

        return new ExternalApiConfiguration
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            BaseUrl = baseUrl.Trim(),
            TimeoutSeconds = timeoutSeconds,
            IsEnabled = true,
            AuthType = authType,
            AuthKeyName = authKeyName?.Trim(),
            AuthKeyLocation = authKeyLocation?.Trim(),
            AuthValue = authValue,
            AuthToken = authToken,
            AuthTokenUrl = authTokenUrl?.Trim(),
            AuthClientId = authClientId,
            AuthClientSecret = authClientSecret,
            AuthScope = authScope?.Trim(),
            AuthAutoRefresh = authAutoRefresh,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateConfig(string baseUrl, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required", nameof(baseUrl));
        if (timeoutSeconds <= 0)
            throw new ArgumentException("Timeout must be positive", nameof(timeoutSeconds));

        BaseUrl = baseUrl.Trim();
        TimeoutSeconds = timeoutSeconds;
        MarkUpdated();
    }

    public void UpdateAuth(
        string authType,
        string? authKeyName = null,
        string? authKeyLocation = null,
        string? authValue = null,
        string? authToken = null,
        string? authTokenUrl = null,
        string? authClientId = null,
        string? authClientSecret = null,
        string? authScope = null,
        bool authAutoRefresh = true)
    {
        AuthType = authType;
        AuthKeyName = authKeyName?.Trim();
        AuthKeyLocation = authKeyLocation?.Trim();
        AuthValue = authValue;
        AuthToken = authToken;
        AuthTokenUrl = authTokenUrl?.Trim();
        AuthClientId = authClientId;
        AuthClientSecret = authClientSecret;
        AuthScope = authScope?.Trim();
        AuthAutoRefresh = authAutoRefresh;
        MarkUpdated();
    }

    public void Enable()
    {
        if (!IsEnabled)
        {
            IsEnabled = true;
            MarkUpdated();
        }
    }

    public void Disable()
    {
        if (IsEnabled)
        {
            IsEnabled = false;
            MarkUpdated();
        }
    }
}
