using System.Net.Http.Headers;
using CustomerSupport.Application.ExternalApis.DTOs;

namespace CustomerSupport.Infrastructure.ExternalApis.Authentication;

/// <summary>
/// Adds API key to outgoing requests (header or query string).
/// </summary>
public class ApiKeyAuthHandler : DelegatingHandler
{
    private readonly string _keyName;
    private readonly string _keyValue;
    private readonly string _keyLocation;

    public ApiKeyAuthHandler(string keyName, string keyValue, string keyLocation)
    {
        _keyName = keyName;
        _keyValue = keyValue;
        _keyLocation = keyLocation;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_keyLocation.Equals("Query", StringComparison.OrdinalIgnoreCase))
        {
            // Add API key as query parameter
            var uriBuilder = new UriBuilder(request.RequestUri!);
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            query[_keyName] = _keyValue;
            uriBuilder.Query = query.ToString();
            request.RequestUri = uriBuilder.Uri;
        }
        else
        {
            // Add API key as header
            request.Headers.TryAddWithoutValidation(_keyName, _keyValue);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Factory for creating ApiKeyAuthHandler.
/// </summary>
public static class ApiKeyAuthHandlerFactory
{
    public static DelegatingHandler Create(ExternalApiAuthConfig authConfig)
    {
        return new ApiKeyAuthHandler(
            authConfig.KeyName,
            authConfig.Value,
            authConfig.KeyLocation);
    }
}
