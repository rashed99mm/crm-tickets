namespace CustomerSupport.Application.Interfaces;

public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateTimeOffset UtcNowOffset { get; }

    DateTime NowInRegion(string? timeZoneId = null);
    DateTimeOffset NowInRegionOffset(string? timeZoneId = null);

    DateTime ToRegionTime(DateTime utc, string? timeZoneId = null);
    DateTime ToUtc(DateTime regionTime, string? timeZoneId = null);

    string Format(DateTime utc, string? format = null, string? culture = null);
    string FormatInRegion(DateTime utc, string? format = null, string? timeZoneId = null, string? culture = null);

    string ToHijri(DateTime utc, string? format = null);
    string ToHijriInRegion(DateTime utc, string? format = null, string? timeZoneId = null);

    TimeZoneInfo GetTimeZone(string? timeZoneId = null);
    TimeSpan GetUtcOffset(string? timeZoneId = null);
}
