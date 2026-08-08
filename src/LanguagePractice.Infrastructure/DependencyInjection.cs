using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Persistence;
using LanguagePractice.Infrastructure.Repositories;
using LanguagePractice.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LanguagePractice.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var provider = DatabaseProvider.Normalize(configuration[$"{DatabaseProvider.SectionName}:Provider"]);
        var connectionString = ResolveConnectionString(configuration, provider, environment);

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (provider == DatabaseProvider.SqlServer)
            {
                // İleride MSSQL'e geçiş: appsettings'te Database:Provider = "SqlServer"
                options.UseSqlServer(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = true;
        });

        services.Configure<ModerationOptions>(configuration.GetSection(ModerationOptions.SectionName));
        services.Configure<SignalingOptions>(configuration.GetSection(SignalingOptions.SectionName));

        var signalingUrl = configuration[$"{SignalingOptions.SectionName}:PublicUrl"] ?? "http://localhost:5050";
        services.AddHttpClient<ISignalingStatsClient, SignalingStatsClient>(client =>
        {
            client.BaseAddress = new Uri(signalingUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        services.AddHttpClient("SignalingModeration", client =>
        {
            client.BaseAddress = new Uri(signalingUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(4);
        });

        services.AddScoped<IMatchHistoryRepository, MatchHistoryRepository>();
        services.AddScoped<IGuestSessionService, GuestSessionService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IModerationService, ModerationService>();
        services.AddScoped<IProfileService, ProfileService>();

        return services;
    }

    private static string ResolveConnectionString(
        IConfiguration configuration,
        string provider,
        IHostEnvironment? environment)
    {
        // Önce provider'a özel connection string, yoksa DefaultConnection
        var namedKey = provider == DatabaseProvider.SqlServer ? "SqlServer" : "Sqlite";
        var connectionString =
            configuration.GetConnectionString(namedKey)
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                $"Connection string '{namedKey}' or 'DefaultConnection' is missing.");

        if (provider == DatabaseProvider.Sqlite)
        {
            connectionString = EnsureSqliteAbsolutePath(connectionString, environment);
        }

        return connectionString;
    }

    /// <summary>
    /// Göreli SQLite yollarını ContentRoot/App_Data altına çözer; klasör yoksa oluşturur.
    /// </summary>
    private static string EnsureSqliteAbsolutePath(string connectionString, IHostEnvironment? environment)
    {
        const string dataSourcePrefix = "Data Source=";
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var updated = new List<string>();

        foreach (var part in parts)
        {
            if (!part.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                updated.Add(part);
                continue;
            }

            var path = part[dataSourcePrefix.Length..].Trim().Trim('"');
            if (!Path.IsPathRooted(path))
            {
                var root = environment?.ContentRootPath ?? Directory.GetCurrentDirectory();
                path = Path.GetFullPath(Path.Combine(root, path));
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            updated.Add($"{dataSourcePrefix}{path}");
        }

        return string.Join(';', updated);
    }
}
