using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CustomerSupport.ExternalApi.Middleware;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        Configuration = configuration;
    }

    private IConfiguration Configuration { get; }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = apiKeyHeaderValues.ToString();

        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var configuredKey = Configuration["ExternalApi__ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            Logger.LogWarning("ExternalApi__ApiKey is not configured — rejecting API-key auth attempt");
            return Task.FromResult(AuthenticateResult.Fail("API key is not configured on the server"));
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);

        if (!CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes))
        {
            Logger.LogWarning("Invalid API key provided from {RemoteIp}",
                Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "api-key-client"),
            new Claim(ClaimTypes.Name, "MachineClient"),
            new Claim("system", "api-key"),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
