using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;

namespace DashboardApi.Services;

/// <summary>
/// Shared HttpClient pool keyed by PAT + timeout.
/// Prevents socket exhaustion from per-request HttpClient creation.
/// </summary>
public static class HttpClientPool
{
    private static readonly ConcurrentDictionary<string, HttpClient> Pool = new();

    /// <summary>
    /// Get or create a pooled HttpClient for the given PAT and timeout.
    /// Do NOT dispose the returned client — it is shared.
    /// </summary>
    public static HttpClient Get(string pat, int timeoutSeconds = 120)
    {
        var key = $"{pat}|{timeoutSeconds}";
        return Pool.GetOrAdd(key, _ =>
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{pat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            return client;
        });
    }

    /// <summary>Dispose and clear all pooled clients — call when PAT changes.</summary>
    public static void Reset()
    {
        foreach (var kv in Pool)
            kv.Value.Dispose();
        Pool.Clear();
    }
}
