using System.Globalization;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Localization;

namespace CustomerSupport.Infrastructure.Localization;

public class LocalizationService : ILocalizationService
{
    private readonly YamlLocalizationStore _store;
    private readonly IUserContext _userContext;

    public LocalizationService(YamlLocalizationStore store, IUserContext userContext)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _userContext = userContext;
    }

    public string GetString(string key, CultureInfo? culture = null)
    {
        culture = GetCultureInfo(culture);
        var lang = culture.TwoLetterISOLanguageName;

        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        if (_store.TryGet(key, out var language) && language != null)
        {
            if (language.TryGetValue(lang, out var v) && !string.IsNullOrEmpty(v)) return v;
            if (language.TryGetValue("ar", out var ar) && !string.IsNullOrEmpty(ar)) return ar;
            return language.Values.FirstOrDefault() ?? key;
        }

        return key;
    }

    public string GetStringOrDefault(string key, string defaultMessage, CultureInfo? culture = null)
    {
        var v = GetString(key, culture);
        return string.IsNullOrEmpty(v) || v == key ? defaultMessage : v;
    }

    public LocalizedMessage GetLocalizedMessage(string key)
    {
        var enCulture = new CultureInfo("en");
        var arCulture = new CultureInfo("ar");

        var enMessage = GetString(key, enCulture);
        var arMessage = GetString(key, arCulture);

        if (string.IsNullOrEmpty(enMessage) || enMessage == key) enMessage = key;
        if (string.IsNullOrEmpty(arMessage) || arMessage == key) arMessage = key;

        return new LocalizedMessage { En = enMessage, Ar = arMessage };
    }

    private CultureInfo GetCultureInfo(CultureInfo? culture)
    {
        if (culture != null) return culture;
        return _userContext?.Locale ?? new CultureInfo("ar-SA");
    }
}
