using System.Net;
using DashboardApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DashboardApi.Tests;

public class CsrfOriginMiddlewareTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "http://localhost:5173",
            "http://127.0.0.1:8080",
        };

        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<CsrfOriginMiddleware>(allowedOrigins);
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

    [Fact]
    public async Task Allowed_Origin_Passes()
    {
        var req = NewWriteRequest();
        req.Headers.Add("Origin", "http://localhost:5173");

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_Localhost_Port_Is_Rejected()
    {
        var req = NewWriteRequest();
        req.Headers.Add("Origin", "http://localhost:5999");

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Allowed_Referer_Fallback_Passes()
    {
        var req = NewWriteRequest();
        req.Headers.Referrer = new Uri("http://localhost:5173/settings");

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage NewWriteRequest()
    {
        return new HttpRequestMessage(HttpMethod.Post, "/api/test")
        {
            Content = new StringContent("{}"),
        };
    }
}