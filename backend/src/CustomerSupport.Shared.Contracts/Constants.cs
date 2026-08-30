namespace CustomerSupport.Shared.Contracts;

public static class Constants
{
    public const string CorsPolicyName = "DefaultCors";
    public const string DefaultCulture = "en";
    public const string ArabicCulture = "ar";
}

public static class RedisHashKeys
{
    public const string UserSessions = "user:sessions";
}

public static class AuthProviders
{
    public const string Jwt = "Jwt";
    public const string Keycloak = "Keycloak";
}
