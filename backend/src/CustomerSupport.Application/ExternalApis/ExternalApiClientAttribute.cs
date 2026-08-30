namespace CustomerSupport.Application.ExternalApis;

[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class ExternalApiClientAttribute : Attribute
{
    public string ApiName { get; }

    public ExternalApiClientAttribute(string apiName)
    {
        ApiName = apiName;
    }
}
