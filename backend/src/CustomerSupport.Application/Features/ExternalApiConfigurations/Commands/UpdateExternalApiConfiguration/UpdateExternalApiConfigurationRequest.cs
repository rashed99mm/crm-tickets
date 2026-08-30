namespace CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.UpdateExternalApiConfiguration;

public record UpdateExternalApiConfigurationRequest(
    string? BaseUrl,
    int? TimeoutSeconds
);
