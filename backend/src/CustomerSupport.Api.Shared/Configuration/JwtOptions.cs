namespace CustomerSupport.Api.Shared.Configuration;

public sealed class JwtOptions
{
    public string? Authority { get; set; }
    public string Audience { get; set; } = "CustomerSupport";
    public string Issuer { get; set; } = "CustomerSupport";
    public string? Key { get; set; }
}
