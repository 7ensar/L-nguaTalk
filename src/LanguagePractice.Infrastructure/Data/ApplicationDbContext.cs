using LanguagePractice.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<UserLanguage> UserLanguages => Set<UserLanguage>();
    public DbSet<MatchHistory> MatchHistories => Set<MatchHistory>();
    public DbSet<GuestSession> GuestSessions => Set<GuestSession>();
    public DbSet<UserReport> UserReports => Set<UserReport>();
    public DbSet<BanRecord> BanRecords => Set<BanRecord>();
    public DbSet<InterestTag> InterestTags => Set<InterestTag>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        SeedLanguages(builder);
    }

    private static void SeedLanguages(ModelBuilder builder)
    {
        builder.Entity<Language>().HasData(
            new Language { Id = 1, Code = "en", Name = "English", NativeName = "English" },
            new Language { Id = 2, Code = "tr", Name = "Turkish", NativeName = "Türkçe" },
            new Language { Id = 3, Code = "de", Name = "German", NativeName = "Deutsch" },
            new Language { Id = 4, Code = "es", Name = "Spanish", NativeName = "Español" },
            new Language { Id = 5, Code = "fr", Name = "French", NativeName = "Français" },
            new Language { Id = 6, Code = "it", Name = "Italian", NativeName = "Italiano" },
            new Language { Id = 7, Code = "ja", Name = "Japanese", NativeName = "日本語" },
            new Language { Id = 8, Code = "ko", Name = "Korean", NativeName = "한국어" },
            new Language { Id = 9, Code = "zh", Name = "Chinese", NativeName = "中文" },
            new Language { Id = 10, Code = "hi", Name = "Hindi", NativeName = "हिन्दी" },
            new Language { Id = 11, Code = "ar", Name = "Arabic", NativeName = "العربية" },
            new Language { Id = 12, Code = "bn", Name = "Bengali", NativeName = "বাংলা" },
            new Language { Id = 13, Code = "pt", Name = "Portuguese", NativeName = "Português" },
            new Language { Id = 14, Code = "ru", Name = "Russian", NativeName = "Русский" },
            new Language { Id = 15, Code = "ur", Name = "Urdu", NativeName = "اردو" },
            new Language { Id = 16, Code = "id", Name = "Indonesian", NativeName = "Bahasa Indonesia" },
            new Language { Id = 17, Code = "sw", Name = "Swahili", NativeName = "Kiswahili" },
            new Language { Id = 18, Code = "vi", Name = "Vietnamese", NativeName = "Tiếng Việt" },
            new Language { Id = 19, Code = "pl", Name = "Polish", NativeName = "Polski" },
            new Language { Id = 20, Code = "nl", Name = "Dutch", NativeName = "Nederlands" }
        );
    }
}
