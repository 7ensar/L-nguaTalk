using System.Threading.RateLimiting;
using LanguagePractice.Infrastructure;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Identity;
using LanguagePractice.Infrastructure.Services;
using LanguagePractice.Web.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Docker / Render / Linux: FileSystemWatcher (inotify) kotası aşılınca
// StartRaisingEvents IOException ile process düşebiliyor.
// CreateBuilder'dan ÖNCE config hot-reload kapatılmalı.
DisableFileWatchersInConstrainedEnvironments();

var builder = WebApplication.CreateBuilder(args);

// CreateBuilder'ın eklediği appsettings kaynaklarında reloadOnChange=false
DisableConfigurationReloadOnChange(builder);

// Gitignore'lı local secrets (ör. Supabase connection string)
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: false);

builder.Services.Configure<SignalingOptions>(
    builder.Configuration.GetSection(SignalingOptions.SectionName));
builder.Services.Configure<ModerationOptions>(
    builder.Configuration.GetSection(ModerationOptions.SectionName));
builder.Services.Configure<AuthExternalOptions>(
    builder.Configuration.GetSection(AuthExternalOptions.SectionName));

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddAppLocalization();
builder.Services.AddControllers();

var googleAuth = builder.Configuration.GetSection("Authentication:Google");
var googleClientId = googleAuth["ClientId"];
var googleClientSecret = googleAuth["ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("reports", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("presence", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddResponseCaching();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

await IdentityDataSeeder.SeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAppLocalization();
app.UseRouting();
app.UseResponseCaching();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static void DisableFileWatchersInConstrainedEnvironments()
{
    var aspEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    var forceDisable = IsTruthy(Environment.GetEnvironmentVariable("DISABLE_FILE_WATCHERS"));

    var inContainer =
        File.Exists("/.dockerenv")
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER"))
        || Directory.Exists("/var/run/secrets/kubernetes.io");

    var isDevelopment = string.Equals(aspEnv, "Development", StringComparison.OrdinalIgnoreCase);

    if (forceDisable || inContainer || !isDevelopment)
    {
        // Host / WebApplication.CreateBuilder appsettings reload
        Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
        // Alternatif anahtar (bazı host sürümleri)
        Environment.SetEnvironmentVariable("HostBuilder__ReloadConfigOnChange", "false");
        // inotify yerine polling (kalan watcher'lar için)
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
    }
}

static void DisableConfigurationReloadOnChange(WebApplicationBuilder builder)
{
    if (builder.Environment.IsDevelopment()
        && !IsTruthy(Environment.GetEnvironmentVariable("DISABLE_FILE_WATCHERS")))
    {
        return;
    }

    foreach (var source in builder.Configuration.Sources)
    {
        if (source is FileConfigurationSource fileSource)
        {
            fileSource.ReloadOnChange = false;
        }
    }
}

static bool IsTruthy(string? value)
    => string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
       || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
       || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
