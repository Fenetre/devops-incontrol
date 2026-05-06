using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace DashboardApi.Services;

/// <summary>Database discovery with TTL-based caching. Supports SQL Server and PostgreSQL.</summary>
public static class DbConnector
{
    private static readonly ConcurrentDictionary<string, (double Timestamp, List<string> Names)> Cache = new();
    private const int CacheTtlSeconds = 300; // 5 minutes

    private static bool TrustSqlServerCertificate()
        => string.Equals(
            Environment.GetEnvironmentVariable("DB_TRUST_SERVER_CERTIFICATE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static SslMode ResolvePostgresSslMode()
    {
        var value = Environment.GetEnvironmentVariable("DB_POSTGRES_SSL_MODE");
        if (string.IsNullOrWhiteSpace(value)) return SslMode.VerifyFull;

        return value.Trim().ToLowerInvariant() switch
        {
            "disable" => SslMode.Disable,
            "allow" => SslMode.Allow,
            "prefer" => SslMode.Prefer,
            "require" => SslMode.Require,
            "verifyca" => SslMode.VerifyCA,
            "verifyfull" => SslMode.VerifyFull,
            _ => SslMode.VerifyFull,
        };
    }

    public static async Task<List<string>> ListDatabaseNamesAsync(string server, int port, string username, string password, string driver = "sqlserver")
    {
        var cacheKey = $"{driver}:{server}:{port}:{username}";
        var now = Environment.TickCount64 / 1000.0;

        if (Cache.TryGetValue(cacheKey, out var entry) && now - entry.Timestamp < CacheTtlSeconds)
            return entry.Names;

        var names = driver == "postgres"
            ? await ListPostgresAsync(server, port, username, password)
            : await ListSqlServerAsync(server, port, username, password);

        Cache[cacheKey] = (now, names);

        return names;
    }

    private static async Task<List<string>> ListSqlServerAsync(string server, int port, string username, string password)
    {
        var csb = new SqlConnectionStringBuilder
        {
            DataSource = $"{server},{port}",
            UserID = username,
            Password = password,
            InitialCatalog = "master",
            Encrypt = true,
            TrustServerCertificate = TrustSqlServerCertificate(),
            ConnectTimeout = 10,
        };

        var names = new List<string>();
        await using var conn = new SqlConnection(csb.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names;
    }

    private static async Task<List<string>> ListPostgresAsync(string server, int port, string username, string password)
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = server,
            Port = port,
            Username = username,
            Password = password,
            Database = "postgres",
            Timeout = 10,
            SslMode = ResolvePostgresSslMode(),
        };

        var names = new List<string>();
        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE datistemplate = false AND datname NOT IN ('postgres') ORDER BY datname", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names;
    }
}
