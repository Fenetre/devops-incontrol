using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;
using DashboardApi.Middleware;

namespace DashboardApi.Tests;

// Static rate limit fields are initialized once — set env vars before any test runs
public class RateLimitMiddlewareTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        // These env vars are read once (static readonly) on first middleware load.
        // Setting them here ensures they apply before the first test host starts.
        Environment.SetEnvironmentVariable("RATE_LIMIT", "20");
        Environment.SetEnvironmentVariable("RATE_WINDOW", "60");
        Environment.SetEnvironmentVariable("TRUST_X_FORWARDED_FOR", "true");
        Environment.SetEnvironmentVariable("TRUSTED_PROXY_IPS", "127.0.0.1,::1");

        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<RateLimitMiddleware>();
                    app.Run(async context =>
                    {
                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsync("OK");
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private HttpRequestMessage WithIp(string ip, string path = "/api/test")
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Forwarded-For", ip);
        return req;
    }

    [Fact]
    public async Task Requests_Within_Limit_Succeed()
    {
        var ip = "10.0.0.1";
        for (int i = 0; i < 15; i++)
        {
            var response = await _client.SendAsync(WithIp(ip));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Requests_Exceeding_Limit_Return_429()
    {
        var ip = "10.0.0.2";

        // Use up the limit (20)
        for (int i = 0; i < 20; i++)
        {
            var response = await _client.SendAsync(WithIp(ip));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Next request should be rate limited
        var blocked = await _client.SendAsync(WithIp(ip));
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Exempt_Paths_Bypass_Rate_Limit()
    {
        var ip = "10.0.0.3";

        // Use up the limit
        for (int i = 0; i < 20; i++)
            await _client.SendAsync(WithIp(ip));

        // Exempt path should still work even though limit is exceeded
        var exempt = await _client.SendAsync(WithIp(ip, "/api/settings/api-key/status"));
        Assert.Equal(HttpStatusCode.OK, exempt.StatusCode);
    }

    [Fact]
    public async Task Concurrent_Requests_Do_Not_Deadlock()
    {
        // Each request from a unique IP — no rate limit hit, tests for deadlocks only
        var tasks = Enumerable.Range(0, 200)
            .Select(i =>
            {
                var req = WithIp($"10.1.{i / 256}.{i % 256}");
                return _client.SendAsync(req);
            })
            .ToArray();

        var completed = await Task.WhenAll(tasks);
        var successCount = completed.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(200, successCount);
    }
}
