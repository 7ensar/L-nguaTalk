using Microsoft.AspNetCore.Localization;

namespace LanguagePractice.Web.Localization;

/// <summary>
/// CDN / proxy country header'larından (GeoIP) dil tahmin eder.
/// CF-IPCountry, CloudFront-Viewer-Country, X-Vercel-IP-Country, X-AppEngine-Country.
/// </summary>
public sealed class GeoIpRequestCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var country =
            FirstHeader(httpContext, "CF-IPCountry")
            ?? FirstHeader(httpContext, "CloudFront-Viewer-Country")
            ?? FirstHeader(httpContext, "X-Vercel-IP-Country")
            ?? FirstHeader(httpContext, "X-AppEngine-Country")
            ?? FirstHeader(httpContext, "X-Country-Code");

        if (string.Equals(country, "XX", StringComparison.OrdinalIgnoreCase)
            || string.Equals(country, "T1", StringComparison.OrdinalIgnoreCase))
        {
            return NullProviderCultureResult;
        }

        var culture = AppCultures.FromCountryCode(country);
        if (culture is null)
        {
            return NullProviderCultureResult;
        }

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }

    private static string? FirstHeader(HttpContext context, string name)
    {
        if (!context.Request.Headers.TryGetValue(name, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
