using System.Security.Cryptography;
using System.Text;
using DashboardApi.Models;
using DashboardApi.Middleware;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(ConfigStore configStore, ILogger<SettingsController> logger) : ControllerBase
{
    private static readonly ILogger AuditLog = LoggerFactory.Create(b => b.AddConsole())
        .CreateLogger("audit");

    private static bool AllowUnprotectedApi()
        => string.Equals(
            Environment.GetEnvironmentVariable("ALLOW_UNPROTECTED_API"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private string ClientIp()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var trustForwardedFor = string.Equals(
            Environment.GetEnvironmentVariable("TRUST_X_FORWARDED_FOR"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (trustForwardedFor)
        {
            var trustedProxyIps = (Environment.GetEnvironmentVariable("TRUSTED_PROXY_IPS") ?? "127.0.0.1,::1")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (remoteIp == "unknown" || trustedProxyIps.Contains(remoteIp))
            {
                var fwd = Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(fwd)) return fwd.Split(',')[0].Trim();
            }
        }

        return remoteIp;
    }

    // --- PAT ---

    public static string? GetPat(ConfigStore store)
    {
        var cred = CredentialManagerService.GetSecret("AZDO_PAT");
        if (!string.IsNullOrEmpty(cred)) return cred;
        var raw = store.LoadConfig().Pat;
        if (string.IsNullOrEmpty(raw)) return null;
        if (!CryptoService.IsEncrypted(raw)) return null;
        return CryptoService.Decrypt(raw);
    }

    [HttpGet("pat/status")]
    public PatStatusResponse PatStatus()
        => new(Configured: !string.IsNullOrEmpty(GetPat(configStore)));

    [HttpPost("pat")]
    public MessageResponse SetPat([FromBody] PatInput body)
    {
        var token = body.Pat.Trim();
        if (!string.IsNullOrEmpty(token))
            CredentialManagerService.SetSecret("AZDO_PAT", token);
        else
            CredentialManagerService.DeleteSecret("AZDO_PAT");

        AuditLog.LogWarning("PAT_CHANGED remote_ip={Ip} configured={Configured}", ClientIp(), !string.IsNullOrEmpty(token));
        AzureDevOpsClient.ResetHttpPool();
        HttpClientPool.Reset();
        return new(string.IsNullOrEmpty(token) ? "PAT cleared" : "PAT saved");
    }

    // --- DB Credentials ---

    public static (string Server, int Port, string Username, string Password, string Driver)? GetDbCredentials(ConfigStore store, int index = 0)
    {
        var cfg = store.LoadConfig();
        if (index < 0 || index >= cfg.DbServers.Count) return null;

        var srv = cfg.DbServers[index];
        if (string.IsNullOrEmpty(srv.Server) || string.IsNullOrEmpty(srv.Username))
            return null;

        var password = CredentialManagerService.GetSecret($"DB_PASSWORD_{index}");
        if (string.IsNullOrEmpty(password))
        {
            // Fall back to index 0 legacy key
            if (index == 0)
                password = CredentialManagerService.GetSecret("DB_PASSWORD");
            if (string.IsNullOrEmpty(password))
            {
                if (string.IsNullOrEmpty(srv.Password)) return null;
                if (!CryptoService.IsEncrypted(srv.Password)) return null;
                password = CryptoService.Decrypt(srv.Password);
            }
        }
        return (srv.Server, srv.Port, srv.Username, password, srv.Driver);
    }

    [HttpGet("db/status")]
    public DbCredentialsStatusResponse DbStatus()
    {
        var cfg = configStore.LoadConfig();
        var servers = new List<DbServerStatusItem>();
        for (int i = 0; i < 3; i++)
        {
            var creds = GetDbCredentials(configStore, i);
            var serverName = i < cfg.DbServers.Count ? cfg.DbServers[i].Server : "";
            var driver = i < cfg.DbServers.Count ? cfg.DbServers[i].Driver : "sqlserver";
            servers.Add(new DbServerStatusItem(i, creds is not null, serverName, driver));
        }
        return new(servers);
    }

    [HttpPost("db")]
    public MessageResponse SetDbCredentials([FromBody] DbCredentialsInput body)
    {
        var config = configStore.LoadConfig();
        var idx = Math.Clamp(body.Index, 0, 2);

        // Ensure the list has enough entries
        while (config.DbServers.Count <= idx)
            config.DbServers.Add(new DbServerConfig());

        config.DbServers[idx].Server = body.Server.Trim();
        config.DbServers[idx].Port = body.Port;
        config.DbServers[idx].Username = body.Username.Trim();
        config.DbServers[idx].Driver = body.Driver.Trim() is "postgres" ? "postgres" : "sqlserver";
        var password = body.Password.Trim();

        if (!string.IsNullOrEmpty(password))
            CredentialManagerService.SetSecret($"DB_PASSWORD_{idx}", password);
        else
            CredentialManagerService.DeleteSecret($"DB_PASSWORD_{idx}");

        config.DbServers[idx].Password = "";
        configStore.SaveConfig(config);
        AuditLog.LogWarning("DB_CREDENTIALS_CHANGED remote_ip={Ip} index={Index} configured={Configured}",
            ClientIp(), idx, !string.IsNullOrEmpty(body.Server.Trim()));
        return new(string.IsNullOrEmpty(body.Server.Trim()) ? $"DB credentials #{idx + 1} cleared" : $"DB credentials #{idx + 1} saved");
    }

    [HttpPost("db/test")]
    public async Task<MessageResponse> TestDbConnectionAsync([FromBody] DbTestInput body, CancellationToken cancellationToken = default)
    {
        var idx = Math.Clamp(body.Index, 0, 2);
        var creds = GetDbCredentials(configStore, idx);
        if (creds is null)
            throw new BadHttpRequestException($"No saved DB credentials for server #{idx + 1}.");

        try
        {
            var dbs = await DbConnector.ListDatabaseNamesAsync(creds.Value.Server, creds.Value.Port,
                creds.Value.Username, creds.Value.Password, creds.Value.Driver);
            return new($"Connection successful — {dbs.Count} database(s) found");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DB connection test failed for server #{Index}", idx);
            throw new BadHttpRequestException("Connection failed. Check server address, port, and credentials.");
        }
    }

    // --- API Key ---

    public static string? GetApiKey(ConfigStore store)
    {
        var cred = CredentialManagerService.GetSecret("API_KEY");
        if (!string.IsNullOrEmpty(cred)) return cred;
        var stored = store.LoadConfig().ApiKey;
        if (string.IsNullOrEmpty(stored)) return null;
        if (!CryptoService.IsEncrypted(stored)) return null;
        return CryptoService.Decrypt(stored);
    }

    [HttpGet("api-key/status")]
    public ApiKeyStatusResponse ApiKeyStatus()
    {
        var key = GetApiKey(configStore);
        var configured = !string.IsNullOrEmpty(key);
        var authenticated = configured && ApiAuthSessionService.IsAuthenticated(Request, key);

        return new(
            Configured: configured,
            AllowUnprotected: AllowUnprotectedApi(),
            Authenticated: authenticated);
    }

    [HttpPost("api-key/verify")]
    public ApiKeyVerifyResponse VerifyApiKey([FromBody] ApiKeyVerifyInput body)
    {
        var expected = GetApiKey(configStore);
        if (string.IsNullOrEmpty(expected))
        {
            ApiAuthSessionService.ClearSessionCookie(Response);
            return new(Valid: false);
        }

        var provided = body.ApiKey.Trim();
        var valid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));

        if (valid)
            ApiAuthSessionService.IssueSessionCookie(Response, Request, expected);

        return new(Valid: valid);
    }

    [HttpPost("api-key")]
    public MessageResponse SetApiKey([FromBody] ApiKeyInput body)
    {
        var key = body.ApiKey.Trim();
        if (!string.IsNullOrEmpty(key))
        {
            CredentialManagerService.SetSecret("API_KEY", key);
            ApiAuthSessionService.IssueSessionCookie(Response, Request, key);
        }
        else
        {
            CredentialManagerService.DeleteSecret("API_KEY");
            ApiAuthSessionService.ClearSessionCookie(Response);
        }

        AuditLog.LogWarning("API_KEY_CHANGED remote_ip={Ip} configured={Configured}",
            ClientIp(), !string.IsNullOrEmpty(key));
        return new(string.IsNullOrEmpty(key) ? "API key cleared" : "API key saved");
    }

    // --- Setup Wizard ---

    [HttpGet("setup-status")]
    public SetupStatusResponse SetupStatus()
    {
        var cfg = configStore.LoadConfig();
        return new(
            SetupComplete: cfg.SetupComplete,
            PatConfigured: !string.IsNullOrEmpty(GetPat(configStore)),
            ProjectCount: cfg.Projects.Count);
    }

    [HttpPost("setup-complete")]
    public MessageResponse CompleteSetup()
    {
        var config = configStore.LoadConfig();
        config.SetupComplete = true;
        configStore.SaveConfig(config);
        return new("Setup complete");
    }

    // --- Email From ---

    [HttpGet("email-from/status")]
    public EmailFromStatusResponse EmailFromStatus()
    {
        var cfg = configStore.LoadConfig();
        return new(Configured: !string.IsNullOrEmpty(cfg.EmailFrom), EmailFrom: cfg.EmailFrom);
    }

    [HttpPost("email-from")]
    public MessageResponse SetEmailFrom([FromBody] EmailFromInput body)
    {
        var config = configStore.LoadConfig();
        config.EmailFrom = body.EmailFrom.Trim();
        configStore.SaveConfig(config);
        AuditLog.LogWarning("EMAIL_FROM_CHANGED remote_ip={Ip} value={Value}", ClientIp(), config.EmailFrom);
        return new(string.IsNullOrEmpty(config.EmailFrom) ? "Email from cleared" : "Email from saved");
    }
}
