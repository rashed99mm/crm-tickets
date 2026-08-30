using CustomerSupport.Api.Shared.Configuration;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace CustomerSupport.Api.Shared.Extensions;

public static class ApiDocumentationExtensions
{
    private const string ApiVersion = "v1";

    public static IServiceCollection AddPlatformOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi(ApiVersion, options =>
        {
            // Publish the XML comments that every project already generates.
            options.AddXmlDocumentation();

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new Microsoft.OpenApi.OpenApiInfo
                {
                    Title = "Customer Support CRM - Internal API v1",
                    Version = ApiVersion,
                    Description = "Customer Support CRM - internal staff API. Clean Architecture, DDD.",
                    Contact = new Microsoft.OpenApi.OpenApiContact
                    {
                        Name = "CustomerSupport Technologies",
                        Email = "support@customersupport.local"
                    }
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Enter your JWT token"
                };

                document.Security ??= new List<OpenApiSecurityRequirement>();
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = new List<string>()
                });

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, _, _) =>
            {
                var parameters = operation.Parameters?.ToList() ?? new List<IOpenApiParameter>();
                parameters.Add(new OpenApiParameter
                {
                    Name = "Accept-Language",
                    In = ParameterLocation.Header,
                    Description = "Language preference (ar, en). Default: ar",
                    Required = false,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
                operation.Parameters = parameters;
                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static IServiceCollection AddPlatformApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    public static WebApplication UsePlatformApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("CustomerSupport Platform API");
            options.AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme);
            options.AddHttpAuthentication(JwtBearerDefaults.AuthenticationScheme, _ => { });
        });

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"/openapi/{ApiVersion}.json", "Customer Support CRM - Internal API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "CustomerSupport Platform API Documentation";
            options.DefaultModelsExpandDepth(2);
            options.EnableDeepLinking();
            options.EnablePersistAuthorization();
        });

        return app;
    }
}
