namespace LanguagePractice.Web.Localization;

public sealed record AppCulture(string Code, string EnglishName, string NativeName, bool IsRtl = false);

/// <summary>
/// Dünyada en çok konuşulan dillere dayalı desteklenen UI dilleri (20).
/// </summary>
public static class AppCultures
{
    public const string Default = "en";
    public const string CookieName = ".LinguaTalk.Culture";

    public static readonly IReadOnlyList<AppCulture> All = new List<AppCulture>
    {
        new("en", "English", "English"),
        new("zh-Hans", "Chinese (Simplified)", "简体中文"),
        new("hi", "Hindi", "हिन्दी"),
        new("es", "Spanish", "Español"),
        new("fr", "French", "Français"),
        new("ar", "Arabic", "العربية", IsRtl: true),
        new("bn", "Bengali", "বাংলা"),
        new("pt", "Portuguese", "Português"),
        new("ru", "Russian", "Русский"),
        new("ur", "Urdu", "اردو", IsRtl: true),
        new("id", "Indonesian", "Bahasa Indonesia"),
        new("de", "German", "Deutsch"),
        new("ja", "Japanese", "日本語"),
        new("sw", "Swahili", "Kiswahili"),
        new("tr", "Turkish", "Türkçe"),
        new("ko", "Korean", "한국어"),
        new("it", "Italian", "Italiano"),
        new("vi", "Vietnamese", "Tiếng Việt"),
        new("pl", "Polish", "Polski"),
        new("nl", "Dutch", "Nederlands")
    };

    public static IReadOnlyList<string> Codes { get; } = All.Select(x => x.Code).ToList();

    public static bool IsSupported(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return false;
        }

        return All.Any(x =>
            x.Code.Equals(culture, StringComparison.OrdinalIgnoreCase)
            || x.Code.StartsWith(culture + "-", StringComparison.OrdinalIgnoreCase)
            || culture.StartsWith(x.Code.Split('-')[0], StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return Default;
        }

        var exact = All.FirstOrDefault(x => x.Code.Equals(culture, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Code;
        }

        var prefix = culture.Split('-', '_')[0];
        if (prefix.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-Hans";
        }

        var byPrefix = All.FirstOrDefault(x =>
            x.Code.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || x.Code.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase));

        return byPrefix?.Code ?? Default;
    }

    public static bool IsRtl(string? culture)
    {
        var code = Normalize(culture);
        return All.Any(x => x.Code == code && x.IsRtl);
    }

    /// <summary>
    /// ISO country code → preferred UI culture (GeoIP / CDN country header).
    /// </summary>
    public static string? FromCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        return countryCode.Trim().ToUpperInvariant() switch
        {
            "US" or "GB" or "AU" or "CA" or "NZ" or "IE" => "en",
            "TR" or "CY" => "tr",
            "ES" or "MX" or "AR" or "CO" or "CL" or "PE" or "VE" => "es",
            "FR" or "BE" or "CH" or "LU" or "MC" or "SN" or "CI" => "fr",
            "DE" or "AT" or "LI" => "de",
            "CN" or "SG" => "zh-Hans",
            "TW" or "HK" or "MO" => "zh-Hans",
            "SA" or "AE" or "EG" or "MA" or "DZ" or "IQ" or "JO" or "KW" or "QA" or "BH" or "OM" or "LB" or "SY" or "TN" or "YE" => "ar",
            "RU" or "BY" or "KZ" or "KG" => "ru",
            "BR" or "PT" or "AO" or "MZ" => "pt",
            "JP" => "ja",
            "IT" or "SM" or "VA" => "it",
            "KR" => "ko",
            "IN" => "hi",
            "BD" => "bn",
            "PK" => "ur",
            "ID" => "id",
            "KE" or "TZ" or "UG" => "sw",
            "VN" => "vi",
            "PL" => "pl",
            "NL" or "SR" => "nl",
            _ => null
        };
    }
}
