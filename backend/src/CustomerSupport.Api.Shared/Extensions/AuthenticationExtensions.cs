using CustomerSupport.Api.Shared.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace CustomerSupport.Api.Shared.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddPlatformAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                if (!string.IsNullOrWhiteSpace(jwtOptions.Authority))
                {
                    options.Authority = jwtOptions.Authority;
                    options.Audience = jwtOptions.Audience;
                }
                else if (!string.IsNullOrWhiteSpace(jwtOptions.Key) && jwtOptions.Key != "CHANGE_IN_PRODUCTION")
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                    };
                }
                else
                {
                    throw new InvalidOperationException(
                        "JWT authentication is enabled but no valid Jwt configuration was found. Set Jwt:Authority or Jwt:Key.");
                }

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs/main"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}
