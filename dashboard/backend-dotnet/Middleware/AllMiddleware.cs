using System.Security.Cryptography;
using System.Text;

namespace DashboardApi.Middleware;

/// <summary>Reject request bodies exceeding MAX_BODY_BYTES on POST/PUT/PATCH.</summary>
public class BodySizeLimitMiddleware(RequestDelegate next)
{
    private static readonly long MaxBytes = long.Parse(
        Environment.GetEnvironmentVariable("MAX_BODY_BYTES") ?? "1048576");

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Method is "POST" or "PUT" or "PATCH")
        {
            if (ctx.Request.ContentLength > MaxBytes)
            {
                ctx.Response.StatusCode = 413;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("""{"detail":"Request entity too large."}""");
                return;
            }

            // Enforce limit on chunked/streaming bodies where Content-Length may be absent
            ctx.Request.EnableBuffering();
            ctx.Request.Body = new LengthLimitingStream(ctx.Request.Body, MaxBytes);
        }

        try
        {
            await next(ctx);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("body size limit"))
        {
            ctx.Response.StatusCode = 413;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"detail":"Request entity too large."}""");
        }
    }
}

/// <summary>Stream wrapper that throws when more than maxBytes are read.</summary>
internal sealed class LengthLimitingStream(Stream inner, long maxBytes) : Stream
{
    private long _totalRead;

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = inner.Read(buffer, offset, count);
        CheckLimit(bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await inner.ReadAsync(buffer, offset, count, cancellationToken);
        CheckLimit(bytesRead);
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await inner.ReadAsync(buffer, cancellationToken);
        CheckLimit(bytesRead);
        return bytesRead;
    }

    private void CheckLimit(int bytesRead)
    {
        _totalRead += bytesRead;
        if (_totalRead > maxBytes)
            throw new InvalidOperationException("Request body size limit exceeded.");
    }

    public override void Flush() => inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>Add security headers and enforce JSON content-type on /api/ responses.</summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    private static readonly string[] CacheablePrefixes =
    [
        "/api/devops/organizations",
        "/api/devops/projects",
        "/api/devops/area-paths",
        "/api/devops/repos",
    ];

    public async Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'";

            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                if (ctx.Request.Method == "GET" && IsCacheable(ctx.Request.Path))
                {
                    ctx.Response.Headers["Cache-Control"] = "private, max-age=300";
                }
                else
                {
                    ctx.Response.Headers["Cache-Control"] = "no-store, no-cache";
                    ctx.Response.Headers["Pragma"] = "no-cache";
                }

                var ct = ctx.Response.ContentType ?? "";
                if (!string.IsNullOrEmpty(ct) && !ct.Contains("json"))
                    ctx.Response.ContentType = "application/json; charset=utf-8";
            }
            return Task.CompletedTask;
        });

        await next(ctx);
    }

    private static bool IsCacheable(PathString path)
    {
        foreach (var prefix in CacheablePrefixes)
            if (path.StartsWithSegments(prefix))
                return true;
        return false;
    }
}

/// <summary>CSRF origin validation for state-changing requests.</summary>
public class CsrfOriginMiddleware(RequestDelegate next, HashSet<string> allowedOrigins)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Method is "POST" or "PUT" or "DELETE" or "PATCH")
        {
            var origin = ctx.Request.Headers.Origin.FirstOrDefault();
            if (string.IsNullOrEmpty(origin))
            {
                // No Origin header — fall back to Referer for CSRF validation
                var referer = ctx.Request.Headers.Referer.FirstOrDefault();
                if (!string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refUri))
                {
                    origin = $"{refUri.Scheme}://{refUri.Authority}";
                }
            }

            if (string.IsNullOrEmpty(origin))
            {
                // Neither Origin nor Referer present — reject the request
                ctx.Response.StatusCode = 403;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("""{"detail":"Missing Origin header."}""");
                return;
            }

            if (!TryNormalizeOrigin(origin, out var normalized) || !allowedOrigins.Contains(normalized))
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("""{"detail":"Origin not allowed."}""");
                return;
            }
        }
        await next(ctx);
    }

    private static bool TryNormalizeOrigin(string origin, out string normalized)
    {
        normalized = string.Empty;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrEmpty(uri.Host))
            return false;

        normalized = $"{uri.Scheme}://{uri.Authority}";
        return true;
    }
}

/// <summary>API key authentication with constant-time comparison.</summary>
public class ApiKeyMiddleware(RequestDelegate next)
{
    private static readonly string[] ExemptPaths =
    [
        "/api/settings/api-key/status",
        "/api/settings/api-key/verify",
        "/api/settings/pat/status",
        "/api/settings/db/status",
        "/api/settings/setup-status",
    ];

    /// <summary>
    /// Paths that are exempt ONLY when no API key has been configured yet (bootstrap).
    /// Once a key exists, these require authentication like any other endpoint.
    /// </summary>
    private static readonly string[] BootstrapPaths =
    [
        "/api/settings/api-key",
        "/api/settings/pat",
        "/api/settings/setup-status",
        "/api/settings/setup-complete",
        "/api/projects",
        "/api/projects/checks/types",
    ];

    /// <summary>
    /// Path prefixes exempt during bootstrap (no API key configured).
    /// Allows the setup wizard to browse orgs/projects before auth is set up.
    /// </summary>
    private static readonly string[] BootstrapPrefixes =
    [
        "/api/devops/organizations",
    ];

    private static bool AllowUnprotectedApi()
        => string.Equals(
            Environment.GetEnvironmentVariable("ALLOW_UNPROTECTED_API"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";

        if (!path.StartsWith("/api/"))
        {
            await next(ctx);
            return;
        }

        if (ExemptPaths.Contains(path))
        {
            await next(ctx);
            return;
        }

        var expected = GetApiKey();

        // Bootstrap: allow setting the API key when none is configured yet
        if (string.IsNullOrEmpty(expected) &&
            (BootstrapPaths.Contains(path) || BootstrapPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))))
        {
            await next(ctx);
            return;
        }

        if (string.IsNullOrEmpty(expected))
        {
            if (AllowUnprotectedApi())
            {
                await next(ctx);
                return;
            }

            ctx.Response.StatusCode = 503;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"detail":"API key is not configured. Configure one in Settings or set ALLOW_UNPROTECTED_API=true for local-only development."}""");
            return;
        }

        // Preferred flow: signed HttpOnly cookie-based auth session.
        if (ApiAuthSessionService.IsAuthenticated(ctx.Request, expected))
        {
            await next(ctx);
            return;
        }

        var provided = ctx.Request.Headers["X-API-Key"].FirstOrDefault() ?? "";
        if (string.IsNullOrEmpty(provided) ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(provided),
                System.Text.Encoding.UTF8.GetBytes(expected)))
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"detail":"Invalid or missing API key."}""");
            return;
        }

        await next(ctx);
    }

    internal static string? GetApiKey()
    {
        var cred = DashboardApi.Services.CredentialManagerService.GetSecret("API_KEY");
        if (!string.IsNullOrEmpty(cred)) return cred;

        var store = new DashboardApi.Services.ConfigStore();
        var cfg = store.LoadConfig();
        if (string.IsNullOrEmpty(cfg.ApiKey)) return null;
        if (!DashboardApi.Services.CryptoService.IsEncrypted(cfg.ApiKey)) return null;
        return DashboardApi.Services.CryptoService.Decrypt(cfg.ApiKey);
    }
}

/// <summary>Per-IP sliding window rate limiting (lock-free).</summary>
public class RateLimitMiddleware(RequestDelegate next)
{
    private static readonly int Window = int.Parse(
        Environment.GetEnvironmentVariable("RATE_WINDOW") ?? "60");
    private static readonly int Limit = int.Parse(
        Environment.GetEnvironmentVariable("RATE_LIMIT") ?? "200");

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentQueue<double>> Hits = new();

    // Periodic cleanup of stale IP entries every 60 seconds
    private static readonly Timer CleanupTimer = new(_ =>
    {
        var now = Environment.TickCount64 / 1000.0;
        foreach (var kvp in Hits)
        {
            // Drain expired entries
            while (kvp.Value.TryPeek(out var oldest) && now - oldest >= Window)
                kvp.Value.TryDequeue(out double _);

            // Remove empty queues
            if (kvp.Value.IsEmpty)
                Hits.TryRemove(kvp.Key, out System.Collections.Concurrent.ConcurrentQueue<double>? _);
        }
    }, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

    private static readonly string[] ExemptPaths =
    [
        "/api/settings/api-key/status",
        "/api/settings/pat/status",
        "/api/settings/db/status",
    ];

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (ExemptPaths.Contains(path))
        {
            await next(ctx);
            return;
        }

        var client = GetClientIp(ctx);
        var now = Environment.TickCount64 / 1000.0;

        var queue = Hits.GetOrAdd(client, _ => new System.Collections.Concurrent.ConcurrentQueue<double>());

        // Drain expired entries for this client
        while (queue.TryPeek(out var oldest) && now - oldest >= Window)
            queue.TryDequeue(out _);

        if (queue.Count >= Limit)
        {
            ctx.Response.StatusCode = 429;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"detail":"Too many requests."}""");
            return;
        }

        queue.Enqueue(now);
        await next(ctx);
    }

    private static string GetClientIp(HttpContext ctx)
    {
        var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var trustForwardedFor = string.Equals(
            Environment.GetEnvironmentVariable("TRUST_X_FORWARDED_FOR"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var trustedProxyIps = (Environment.GetEnvironmentVariable("TRUSTED_PROXY_IPS") ?? "127.0.0.1,::1")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var trustedProxy = remoteIp == "unknown" || trustedProxyIps.Contains(remoteIp);
        if (trustForwardedFor && trustedProxy)
        {
            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
                return forwarded.Split(',')[0].Trim();
        }

        return remoteIp;
    }
}

/// <summary>
/// Stateless signed auth cookie for API key sessions.
/// This avoids storing the API key itself in browser storage.
/// </summary>
public static class ApiAuthSessionService
{
    private const string CookieName = "pm_auth";
    private const string TokenVersion = "v1";

    public static bool IsAuthenticated(HttpRequest request, string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return false;
        if (!request.Cookies.TryGetValue(CookieName, out var token) || string.IsNullOrEmpty(token))
            return false;

        return ValidateToken(token, apiKey, request);
    }

    public static void IssueSessionCookie(HttpResponse response, HttpRequest request, string apiKey)
    {
        var days = GetSessionDays();
        var token = CreateToken(apiKey, request, days);
        var options = BuildCookieOptions(days);
        response.Cookies.Append(CookieName, token, options);
    }

    public static void ClearSessionCookie(HttpResponse response)
    {
        // Delete both secure and non-secure variants to ensure cleanup regardless of environment.
        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/", SameSite = SameSiteMode.Strict, Secure = true, HttpOnly = true });
        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/", SameSite = SameSiteMode.Strict, Secure = false, HttpOnly = true });
    }

    private static CookieOptions BuildCookieOptions(int days)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(days),
            IsEssential = true,
        };
    }

    private static int GetSessionDays()
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("API_AUTH_SESSION_DAYS"), out var days))
            days = 7;

        return Math.Clamp(days, 1, 365);
    }

    private static string CreateToken(string apiKey, HttpRequest request, int days)
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(days).ToUnixTimeSeconds();
        var nonce = ToBase64Url(RandomNumberGenerator.GetBytes(16));
        var userAgentHash = HashText(request.Headers.UserAgent.FirstOrDefault() ?? "");

        var payload = $"{TokenVersion}|{expiry}|{userAgentHash}|{nonce}";
        var payloadEncoded = ToBase64Url(Encoding.UTF8.GetBytes(payload));
        var signature = ComputeSignature(payloadEncoded, apiKey);

        return $"{payloadEncoded}.{signature}";
    }

    private static bool ValidateToken(string token, string apiKey, HttpRequest request)
    {
        var parts = token.Split('.');
        if (parts.Length != 2) return false;

        var payloadEncoded = parts[0];
        var providedSignature = parts[1];
        var expectedSignature = ComputeSignature(payloadEncoded, apiKey);
        if (!FixedEquals(providedSignature, expectedSignature)) return false;

        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(FromBase64Url(payloadEncoded));
        }
        catch
        {
            return false;
        }

        var fields = payload.Split('|');
        if (fields.Length != 4) return false;
        if (!string.Equals(fields[0], TokenVersion, StringComparison.Ordinal)) return false;
        if (!long.TryParse(fields[1], out var expiryUnix)) return false;
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiryUnix) return false;

        var expectedUserAgentHash = HashText(request.Headers.UserAgent.FirstOrDefault() ?? "");
        if (!string.Equals(fields[2], expectedUserAgentHash, StringComparison.Ordinal)) return false;

        return true;
    }

    private static string ComputeSignature(string payloadEncoded, string apiKey)
    {
        using var hmac = new HMACSHA256(DeriveSigningKey(apiKey));
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadEncoded));
        return ToBase64Url(sig);
    }

    private static byte[] DeriveSigningKey(string apiKey)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes($"pm-auth-session|{apiKey}"));
    }

    private static string HashText(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash);
    }

    private static bool FixedEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        if (left.Length != right.Length) return false;
        return CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] FromBase64Url(string value)
    {
        var b64 = value.Replace('-', '+').Replace('_', '/');
        var pad = b64.Length % 4;
        if (pad > 0) b64 += new string('=', 4 - pad);
        return Convert.FromBase64String(b64);
    }
}

/// <summary>
/// Catches AzureDevOpsPatScopeException from any controller/service
/// and returns a 403 with a user-friendly PAT scope message.
/// </summary>
public class PatScopeExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (DashboardApi.Services.AzureDevOpsPatScopeException ex)
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json";
            var escaped = System.Text.Json.JsonSerializer.Serialize(ex.Message);
            await ctx.Response.WriteAsync($"{{\"detail\":{escaped}}}");
        }
    }
}
