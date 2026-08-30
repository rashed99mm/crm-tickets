using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.ExternalApis.Authentication;

public class OAuth2ClientCredentialsHandler : DelegatingHandler
{
    private readonly string _tokenUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _scope;
    private readonly bool _autoRefresh;
    private readonly ILogger<OAuth2ClientCredentialsHandler> _logger;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public OAuth2ClientCredentialsHandler(
        string tokenUrl,
        string clientId,
        string clientSecret,
        string scope,
        bool autoRefresh,
        ILogger<OAuth2ClientCredentialsHandler> logger)
    {
        _tokenUrl = tokenUrl;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _scope = scope;
        _autoRefresh = autoRefresh;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_accessToken) || (_autoRefresh && DateTime.UtcNow >= _tokenExpiry.AddSeconds(-60)))
        {
            await AcquireTokenAsync(cancellationToken);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task AcquireTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = new HttpClient();
            var requestContent = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            };

            if (!string.IsNullOrEmpty(_scope))
            {
                requestContent["scope"] = _scope;
            }

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _tokenUrl)
            {
                Content = new FormUrlEncodedContent(requestContent)
            };

            var response = await httpClient.SendAsync(tokenRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse != null)
            {
                _accessToken = tokenResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
                _logger.LogDebug("OAuth2 token acquired, expires at {Expiry}", _tokenExpiry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire OAuth2 token");
            throw;
        }
    }
}

public class OAuthTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; } = 3600;
    public string? Scope { get; set; }
}

public static class OAuth2ClientCredentialsHandlerFactory
{
    public static DelegatingHandler Create(
        string tokenUrl,
        string clientId,
        string clientSecret,
        string scope,
        bool autoRefresh,
        ILoggerFactory loggerFactory)
    {
        return new OAuth2ClientCredentialsHandler(
            tokenUrl,
            clientId,
            clientSecret,
            scope,
            autoRefresh,
            loggerFactory.CreateLogger<OAuth2ClientCredentialsHandler>());
    }
}
