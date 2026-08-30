using System.Net.Http.Headers;

namespace CustomerSupport.Infrastructure.ExternalApis.Authentication;

public class BearerTokenAuthHandler : DelegatingHandler
{
    private readonly string _token;

    public BearerTokenAuthHandler(string token)
    {
        _token = token;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return base.SendAsync(request, cancellationToken);
    }
}

public static class BearerTokenAuthHandlerFactory
{
    public static DelegatingHandler Create(string token)
    {
        return new BearerTokenAuthHandler(token);
    }
}
