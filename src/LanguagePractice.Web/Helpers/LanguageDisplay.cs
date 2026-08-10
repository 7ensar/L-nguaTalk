namespace LanguagePractice.Web.Helpers;

public sealed record PracticeLanguage(string Code, string Name);

public static class LanguageDisplay
{
    private static readonly IReadOnlyDictionary<string, string> CountryCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "gb",
        ["tr"] = "tr",
        ["de"] = "de",
        ["es"] = "es",
        ["fr"] = "fr",
        ["it"] = "it",
        ["ja"] = "jp",
        ["ko"] = "kr",
        ["zh"] = "cn",
        ["hi"] = "in",
        ["ar"] = "sa",
        ["bn"] = "bd",
        ["pt"] = "pt",
        ["ru"] = "ru",
        ["ur"] = "pk",
        ["id"] = "id",
        ["sw"] = "ke",
        ["vi"] = "vn",
        ["pl"] = "pl",
        ["nl"] = "nl"
    };

    /// <summary>Ana sayfa / lobi dil kutucukları.</summary>
    public static IReadOnlyList<PracticeLanguage> PracticeOptions { get; } =
    [
        new("en", "English"),
        new("tr", "Türkçe"),
        new("es", "Español"),
        new("fr", "Français"),
        new("de", "Deutsch"),
        new("zh", "中文"),
        new("ar", "العربية"),
        new("ru", "Русский"),
        new("pt", "Português"),
        new("ja", "日本語"),
        new("it", "Italiano"),
        new("ko", "한국어"),
        new("hi", "हिन्दी")
    ];

    /// <summary>ISO 3166-1 alpha-2 country code for flag images (flagcdn).</summary>
    public static string? CountryCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        var raw = languageCode.Trim();
        if (CountryCodes.TryGetValue(raw, out var country))
        {
            return country;
        }

        // zh-Hans, en-US vb. → dil öneki
        var prefix = raw.Split('-', '_')[0];
        return CountryCodes.TryGetValue(prefix, out country) ? country : null;
    }

    public static string FlagImageUrl(string? languageCode, int width = 40)
    {
        // w20 / w40 / w80 desteklenir; geçersiz genişlikte kırılmayı önle
        var w = width switch
        {
            <= 20 => 20,
            <= 40 => 40,
            _ => 80
        };

        var country = CountryCode(languageCode) ?? "un";
        return $"https://flagcdn.com/w{w}/{country}.png";
    }

    public static int CountFor(IReadOnlyDictionary<string, int>? counts, string code)
    {
        if (counts is null || string.IsNullOrWhiteSpace(code))
        {
            return 0;
        }

        return counts.TryGetValue(code, out var n) ? n : 0;
    }
}
