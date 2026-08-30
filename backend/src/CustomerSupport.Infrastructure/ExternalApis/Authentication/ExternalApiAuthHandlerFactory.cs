using CustomerSupport.Application.ExternalApis.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomerSupport.Infrastructure.ExternalApis.Authentication;

public static class ExternalApiAuthHandlerFactory
{
    public static DelegatingHandler? Create(ExternalApiAuthConfig authConfig, ILoggerFactory? loggerFactory = null)
    {
        if (authConfig == null || authConfig.Type == ExternalApiAuthType.None)
        {
            return null;
        }

        var logger = loggerFactory ?? NullLoggerFactory.Instance;

        return authConfig.Type switch
        {
            ExternalApiAuthType.ApiKey => ApiKeyAuthHandlerFactory.Create(authConfig),
            ExternalApiAuthType.Bearer => BearerTokenAuthHandlerFactory.Create(authConfig.Token),
            ExternalApiAuthType.Basic => BasicAuthHandlerFactory.Create(authConfig.ClientId, authConfig.ClientSecret),
            ExternalApiAuthType.OAuth2 => OAuth2ClientCredentialsHandlerFactory.Create(
                authConfig.TokenUrl,
                authConfig.ClientId,
                authConfig.ClientSecret,
                authConfig.Scope,
                authConfig.AutoRefresh,
                logger),
            _ => null
        };
    }
}
