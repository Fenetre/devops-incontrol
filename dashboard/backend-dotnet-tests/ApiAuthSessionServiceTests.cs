using System.Linq;
using DashboardApi.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DashboardApi.Tests;

public class ApiAuthSessionServiceTests
{
    [Fact]
    public void IssueCookie_AlwaysSetsSecureFlag()
    {
        var previous = Environment.GetEnvironmentVariable("AUTH_COOKIE_SECURE");
        Environment.SetEnvironmentVariable("AUTH_COOKIE_SECURE", "false");

        try
        {
            var ctx = NewContext("test-agent/1.0");

            ApiAuthSessionService.IssueSessionCookie(ctx.Response, ctx.Request, "secret-key");
            var header = ExtractSetCookieHeader(ctx.Response, "pm_auth");

            Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTH_COOKIE_SECURE", previous);
        }
    }

    [Fact]
    public void IssueCookie_ThenAuthenticate_WithSameUserAgent_ReturnsTrue()
    {
        var ctx = NewContext("test-agent/1.0");

        ApiAuthSessionService.IssueSessionCookie(ctx.Response, ctx.Request, "secret-key");
        var cookie = ExtractCookieValue(ctx.Response, "pm_auth");

        var reqCtx = NewContext("test-agent/1.0");
        reqCtx.Request.Headers.Cookie = $"pm_auth={cookie}";

        var ok = ApiAuthSessionService.IsAuthenticated(reqCtx.Request, "secret-key");
        Assert.True(ok);
    }

    [Fact]
    public void Authenticate_WithTamperedCookie_ReturnsFalse()
    {
        var ctx = NewContext("test-agent/1.0");

        ApiAuthSessionService.IssueSessionCookie(ctx.Response, ctx.Request, "secret-key");
        var cookie = ExtractCookieValue(ctx.Response, "pm_auth");

        // Tamper the token payload/signature boundary to invalidate HMAC.
        var tampered = cookie + "x";
        var reqCtx = NewContext("test-agent/1.0");
        reqCtx.Request.Headers.Cookie = $"pm_auth={tampered}";

        var ok = ApiAuthSessionService.IsAuthenticated(reqCtx.Request, "secret-key");
        Assert.False(ok);
    }

    [Fact]
    public void Authenticate_WithDifferentUserAgent_ReturnsFalse()
    {
        var ctx = NewContext("test-agent/1.0");

        ApiAuthSessionService.IssueSessionCookie(ctx.Response, ctx.Request, "secret-key");
        var cookie = ExtractCookieValue(ctx.Response, "pm_auth");

        var reqCtx = NewContext("different-agent/2.0");
        reqCtx.Request.Headers.Cookie = $"pm_auth={cookie}";

        var ok = ApiAuthSessionService.IsAuthenticated(reqCtx.Request, "secret-key");
        Assert.False(ok);
    }

    private static DefaultHttpContext NewContext(string userAgent)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.Headers.UserAgent = userAgent;
        return ctx;
    }

    private static string ExtractCookieValue(HttpResponse response, string cookieName)
    {
        var header = ExtractSetCookieHeader(response, cookieName);
        var firstPart = header.Split(';', 2)[0];
        var idx = firstPart.IndexOf('=');
        Assert.True(idx > 0);
        return firstPart[(idx + 1)..];
    }

    private static string ExtractSetCookieHeader(HttpResponse response, string cookieName)
    {
        var setCookieHeaders = response.Headers.SetCookie;
        var header = setCookieHeaders.FirstOrDefault(h =>
            !string.IsNullOrEmpty(h) && h.StartsWith(cookieName + "="));
        Assert.False(string.IsNullOrWhiteSpace(header));
        return header!;
    }
}
