using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CustomerSupport.Infrastructure.Localization;

public class YamlLocalizationStore
{
    private readonly Dictionary<string, Dictionary<string, string>> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public YamlLocalizationStore()
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var location = asm.Location;
                if (string.IsNullOrEmpty(location)) continue;
                var dir = Path.GetDirectoryName(location);
                if (string.IsNullOrEmpty(dir)) continue;

                var resourcesPath = Path.Combine(dir, "Localization", "Resources.yaml");
                if (File.Exists(resourcesPath))
                {
                    var resourcesYaml = File.ReadAllText(resourcesPath);
                    var resourcesParsed = deserializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(resourcesYaml);
                    Merge(resourcesParsed);
                }
            }
            catch
            {
                // Continue loading other assemblies on malformed files
            }
        }
    }

    private void Merge(Dictionary<string, Dictionary<string, string>>? parsed)
    {
        if (parsed == null) return;
        lock (_lock)
        {
            foreach (var kv in parsed)
            {
                var key = kv.Key.Trim();
                if (!_store.TryGetValue(key, out var langs))
                {
                    langs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _store[key] = langs;
                }

                foreach (var lp in kv.Value)
                {
                    var lang = lp.Key.Trim();
                    var text = lp.Value ?? string.Empty;
                    langs[lang] = text;
                }
            }
        }
    }

    public bool TryGet(string key, out Dictionary<string, string>? langs)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            langs = null;
            return false;
        }
        return _store.TryGetValue(key, out langs!);
    }
}
