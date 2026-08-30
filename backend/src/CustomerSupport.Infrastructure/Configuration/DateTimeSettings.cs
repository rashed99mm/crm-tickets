namespace CustomerSupport.Infrastructure.Configuration;

public sealed class DateTimeSettings
{
    public string DefaultTimeZoneId { get; set; } = "Asia/Riyadh";
    public string DefaultCulture { get; set; } = "ar-SA";
    public string DefaultDateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
    public string HijriDateFormat { get; set; } = "dd MMMM yyyy";
    public List<string> SupportedTimeZones { get; set; } = new() { "Asia/Riyadh", "UTC" };
}
