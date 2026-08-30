using CustomerSupport.Application.ExternalApis.DTOs;

namespace CustomerSupport.Application.Interfaces;

public interface IExternalApiConfigurationProvider
{
    ExternalApiConfig? GetConfig(string apiName);
    IReadOnlyList<ExternalApiConfig> GetAllConfigs();
    Task ReloadAsync(CancellationToken ct = default);
}
