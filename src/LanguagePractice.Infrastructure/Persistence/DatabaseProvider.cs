namespace LanguagePractice.Infrastructure.Persistence;

/// <summary>
/// Geçici SQLite ve hedef MSSQL arasında geçiş için provider anahtarları.
/// appsettings: "Database:Provider" = "Sqlite" | "SqlServer"
/// </summary>
public static class DatabaseProvider
{
    public const string SectionName = "Database";
    public const string Sqlite = "Sqlite";
    public const string SqlServer = "SqlServer";

    public static string Normalize(string? provider)
    {
        if (string.Equals(provider, SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            return SqlServer;
        }

        // Varsayılan: Mac geliştirme için SQLite
        return Sqlite;
    }
}
