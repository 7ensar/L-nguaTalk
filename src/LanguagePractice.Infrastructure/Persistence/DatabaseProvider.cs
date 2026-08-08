namespace LanguagePractice.Infrastructure.Persistence;

/// <summary>
/// Veritabanı provider anahtarları.
/// appsettings: "Database:Provider" = "Sqlite" | "PostgreSQL" | "SqlServer"
/// </summary>
public static class DatabaseProvider
{
    public const string SectionName = "Database";
    public const string Sqlite = "Sqlite";
    public const string PostgreSQL = "PostgreSQL";
    public const string SqlServer = "SqlServer";

    public static string Normalize(string? provider)
    {
        if (string.Equals(provider, SqlServer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "MSSQL", StringComparison.OrdinalIgnoreCase))
        {
            return SqlServer;
        }

        if (string.Equals(provider, PostgreSQL, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return PostgreSQL;
        }

        return Sqlite;
    }
}
