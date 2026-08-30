using CustomerSupport.Application.ExternalApis.DTOs;

namespace CustomerSupport.Infrastructure.ExternalApis.Configuration;

public class ExternalApiOptions
{
    public const string SectionName = "ExternalApis";

    public Dictionary<string, ExternalApiConfig> Apis { get; set; } = new();
}
