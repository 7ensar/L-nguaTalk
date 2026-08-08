using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace LanguagePractice.Web.Localization;

public sealed class JsonStringLocalizer : IStringLocalizer
{
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _resourcesPath;
    private readonly string _culture;

    public JsonStringLocalizer(string resourcesPath, CultureInfo culture)
    {
        _resourcesPath = resourcesPath;
        _culture = AppCultures.Normalize(culture.Name);
    }

    public LocalizedString this[string name]
    {
        get
        {
            var value = GetString(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var format = GetString(name) ?? name;
            string value;
            try
            {
                value = string.Format(CultureInfo.CurrentCulture, format, arguments);
            }
            catch (FormatException)
            {
                value = format;
            }

            return new LocalizedString(name, value, resourceNotFound: GetString(name) is null);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        foreach (var pair in Load(_culture))
        {
            yield return new LocalizedString(pair.Key, pair.Value, resourceNotFound: false);
        }
    }

    private string? GetString(string name)
    {
        if (Load(_culture).TryGetValue(name, out var value))
        {
            return value;
        }

        if (!_culture.Equals(AppCultures.Default, StringComparison.OrdinalIgnoreCase)
            && Load(AppCultures.Default).TryGetValue(name, out var fallback))
        {
            return fallback;
        }

        return null;
    }

    private IReadOnlyDictionary<string, string> Load(string culture)
    {
        return Cache.GetOrAdd(culture, code =>
        {
            var path = Path.Combine(_resourcesPath, $"{code}.json");
            if (!File.Exists(path))
            {
                var prefix = code.Split('-')[0];
                path = Path.Combine(_resourcesPath, $"{prefix}.json");
            }

            if (!File.Exists(path))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            using var stream = File.OpenRead(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                       ?? new Dictionary<string, string>();
            return new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
        });
    }
}
