namespace LanguagePractice.Web.Helpers;

public sealed record PracticeLanguage(string Code, string Name);

public static class LanguageDisplay
{
    private static readonly IReadOnlyDictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "🇬🇧",
        ["tr"] = "🇹🇷",
        ["de"] = "🇩🇪",
        ["es"] = "🇪🇸",
        ["fr"] = "🇫🇷",
        ["it"] = "🇮🇹",
        ["ja"] = "🇯🇵",
        ["ko"] = "🇰🇷",
        ["zh"] = "🇨🇳",
        ["hi"] = "🇮🇳",
        ["ar"] = "🇸🇦",
        ["bn"] = "🇧🇩",
        ["pt"] = "🇵🇹",
        ["ru"] = "🇷🇺",
        ["ur"] = "🇵🇰",
        ["id"] = "🇮🇩",
        ["sw"] = "🇰🇪",
        ["vi"] = "🇻🇳",
        ["pl"] = "🇵🇱",
        ["nl"] = "🇳🇱"
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

    public static string Flag(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "🌐";
        }

        return Flags.TryGetValue(code.Trim(), out var flag) ? flag : "🌐";
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
