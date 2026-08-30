using System.Globalization;

namespace CustomerSupport.Application.Localization;

public interface ILocalizationService
{
    string GetString(string key, CultureInfo? culture = null);
    string GetStringOrDefault(string key, string defaultMessage, CultureInfo? culture = null);
    LocalizedMessage GetLocalizedMessage(string key);
}
