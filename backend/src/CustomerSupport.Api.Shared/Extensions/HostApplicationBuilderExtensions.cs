using Serilog;

namespace CustomerSupport.Api.Shared.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static IHostBuilder AddPlatformLogging(this IHostBuilder hostBuilder, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Application", "CustomerSupport.InternalApi")
            .CreateLogger();

        hostBuilder.UseSerilog();
        return hostBuilder;
    }
}
