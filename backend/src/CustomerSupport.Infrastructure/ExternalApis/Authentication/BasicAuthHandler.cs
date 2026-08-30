using System.Net.Http.Headers;
using System.Text;

namespace CustomerSupport.Infrastructure.ExternalApis.Authentication;

public class BasicAuthHandler : DelegatingHandler
{
    private readonly string _username;
    private readonly string _password;

    public BasicAuthHandler(string username, string password)
    {
        _username = username;
        _password = password;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return base.SendAsync(request, cancellationToken);
    }
}

public static class BasicAuthHandlerFactory
{
    public static DelegatingHandler Create(string username, string password)
    {
        return new BasicAuthHandler(username, password);
    }
}
