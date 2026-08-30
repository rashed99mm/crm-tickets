namespace CustomerSupport.Application.ExternalApis.DTOs;

/// <summary>
/// Configuration for an external API endpoint.
/// </summary>
public class ExternalApiConfig
{
    /// <summary>Base URL of the external API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Authentication configuration for this API.</summary>
    public ExternalApiAuthConfig Auth { get; set; } = new();

    /// <summary>Request timeout in seconds. Defaults to 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Authentication settings for external API access.
/// </summary>
public class ExternalApiAuthConfig
{
    /// <summary>Authentication type (None, ApiKey, Bearer, Basic, OAuth2).</summary>
    public ExternalApiAuthType Type { get; set; } = ExternalApiAuthType.None;

    // ApiKey settings
    /// <summary>Header or query parameter name for API key.</summary>
    public string KeyName { get; set; } = string.Empty;
    /// <summary>Where to send the key (Header or Query).</summary>
    public string KeyLocation { get; set; } = "Header";
    /// <summary>The API key value.</summary>
    public string Value { get; set; } = string.Empty;

    // Bearer token settings
    /// <summary>Bearer token for API authentication.</summary>
    public string Token { get; set; } = string.Empty;

    // OAuth2 settings
    /// <summary>OAuth2 token endpoint URL.</summary>
    public string TokenUrl { get; set; } = string.Empty;
    /// <summary>OAuth2 client ID.</summary>
    public string ClientId { get; set; } = string.Empty;
    /// <summary>OAuth2 client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>OAuth2 scope.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>Auto-refresh token before expiry.</summary>
    public bool AutoRefresh { get; set; } = true;
}

/// <summary>
/// Supported authentication types for external APIs.
/// </summary>
public enum ExternalApiAuthType
{
    /// <summary>No authentication.</summary>
    None,
    /// <summary>API key in header or query string.</summary>
    ApiKey,
    /// <summary>Bearer token (JWT).</summary>
    Bearer,
    /// <summary>Basic authentication.</summary>
    Basic,
    /// <summary>OAuth2 client credentials.</summary>
    OAuth2
}
