using CustomerSupport.Application.Interfaces;
using CustomerSupport.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace CustomerSupport.Infrastructure.Services;

public class DateTimeService : IDateTimeService
{
    private readonly DateTimeSettings _settings;
    private readonly UmAlQuraCalendar _hijriCalendar = new();

    public DateTimeService(IOptions<DateTimeSettings> settings)
    {
        _settings = settings.Value;
    }

    public DateTime UtcNow => DateTime.UtcNow;
    public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;

    public DateTime NowInRegion(string? timeZoneId = null)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTimeZone(timeZoneId));

    public DateTimeOffset NowInRegionOffset(string? timeZoneId = null)
        => new(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTimeZone(timeZoneId)),
               GetTimeZone(timeZoneId).GetUtcOffset(DateTime.UtcNow));

    public DateTime ToRegionTime(DateTime utc, string? timeZoneId = null)
    {
        if (utc.Kind == DateTimeKind.Local)
            throw new ArgumentException("Expected UTC DateTime but received Local kind.", nameof(utc));

        if (utc.Kind == DateTimeKind.Unspecified)
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, GetTimeZone(timeZoneId));
    }

    public DateTime ToUtc(DateTime regionTime, string? timeZoneId = null)
    {
        if (regionTime.Kind == DateTimeKind.Utc)
            return regionTime;

        var tz = GetTimeZone(timeZoneId);
        var utc = TimeZoneInfo.ConvertTimeToUtc(
            regionTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(regionTime, DateTimeKind.Unspecified)
                : regionTime,
            tz);

        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    public string Format(DateTime utc, string? format = null, string? culture = null)
    {
        var ci = GetCulture(culture);
        return utc.ToString(format ?? _settings.DefaultDateFormat, ci);
    }

    public string FormatInRegion(DateTime utc, string? format = null, string? timeZoneId = null, string? culture = null)
    {
        var regionTime = ToRegionTime(utc, timeZoneId);
        var ci = GetCulture(culture);
        return regionTime.ToString(format ?? _settings.DefaultDateFormat, ci);
    }

    public string ToHijri(DateTime utc, string? format = null)
    {
        var hijriFormat = format ?? _settings.HijriDateFormat;
        return ToHijriDate(utc, hijriFormat);
    }

    public string ToHijriInRegion(DateTime utc, string? format = null, string? timeZoneId = null)
    {
        var regionTime = ToRegionTime(utc, timeZoneId);
        var hijriFormat = format ?? _settings.HijriDateFormat;
        return ToHijriDate(regionTime, hijriFormat);
    }

    public TimeZoneInfo GetTimeZone(string? timeZoneId = null)
    {
        var id = timeZoneId ?? _settings.DefaultTimeZoneId;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            if (!_settings.SupportedTimeZones.Contains(id))
                throw new InvalidOperationException($"Time zone '{id}' is not supported.");

            var offset = id switch
            {
                "Asia/Riyadh" => TimeSpan.FromHours(3),
                "Asia/Dubai" => TimeSpan.FromHours(4),
                "UTC" => TimeSpan.Zero,
                _ => TimeSpan.FromHours(3)
            };

            return TimeZoneInfo.CreateCustomTimeZone(id, offset, id, id);
        }
    }

    public TimeSpan GetUtcOffset(string? timeZoneId = null)
        => GetTimeZone(timeZoneId).GetUtcOffset(DateTime.UtcNow);

    private CultureInfo GetCulture(string? culture)
    {
        try
        {
            return new CultureInfo(culture ?? _settings.DefaultCulture);
        }
        catch (CultureNotFoundException)
        {
            return new CultureInfo("ar-SA");
        }
    }

    private string ToHijriDate(DateTime date, string format)
    {
        var ci = new CultureInfo("ar-SA")
        {
            DateTimeFormat = { Calendar = _hijriCalendar }
        };

        return date.ToString(format, ci);
    }
}
