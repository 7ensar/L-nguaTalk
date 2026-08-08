using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace LanguagePractice.Web.Localization;

public static class LocalizationExtensions
{
    public static IMvcBuilder AddAppLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();

        services.Configure<RequestLocalizationOptions>(options =>
        {
            var cultures = AppCultures.Codes
                .Select(code => new CultureInfo(code))
                .ToList();

            options.DefaultRequestCulture = new RequestCulture(AppCultures.Default);
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.ApplyCurrentCultureToResponseHeaders = true;

            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider { CookieName = AppCultures.CookieName },
                new GeoIpRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
        });

        return services.AddControllersWithViews()
            .AddViewLocalization()
            .AddDataAnnotationsLocalization(options =>
            {
                options.DataAnnotationLocalizerProvider = (_, factory) =>
                    factory.Create(typeof(SharedResources));
            });
    }

    public static IApplicationBuilder UseAppLocalization(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<RequestLocalizationOptions>>();
        app.UseRequestLocalization(options.Value);
        return app;
    }
}
