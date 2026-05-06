using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DashboardApi.Services;

/// <summary>
/// Azure DevOps REST client with retry, caching, and batch operations.
/// Port of Python app/connectors/azure_devops.py.
/// </summary>
public class AzureDevOpsClient
{
    private static readonly ConcurrentDictionary<string, (long Ticks, List<Dictionary<string, object?>> Data)> IterationsCache = new();
    private static readonly ConcurrentDictionary<string, (long Ticks, JsonElement Data)> WorkItemsCache = new();
    private static readonly ConcurrentDictionary<string, (long Ticks, List<JsonElement> Data)> PrCache = new();
    private static readonly ConcurrentDictionary<string, HttpClient> HttpPool = new();

    private static readonly long IterationsTtlTicks = TimeSpan.FromMinutes(5).Ticks;
    private static readonly long WorkItemsTtlTicks = TimeSpan.FromMinutes(10).Ticks;
    private static readonly long PrCacheTtlTicks = TimeSpan.FromMinutes(5).Ticks;

    private readonly HttpClient _http;
    private readonly string _organization;
    private readonly string _project;
    private readonly string _apiVersion;

    public string BaseUrl => $"https://dev.azure.com/{_organization}/{_project}/_apis";

    public AzureDevOpsClient(string organization, string project, string pat, string apiVersion = "7.1", int timeoutSeconds = 45)
    {
        _organization = organization;
        _project = project;
        _apiVersion = apiVersion;

        var poolKey = $"{organization}|{pat}";
        _http = HttpPool.GetOrAdd(poolKey, _ =>
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{pat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            return client;
        });
    }

    public static void ClearRunCaches()
    {
        IterationsCache.Clear();
        WorkItemsCache.Clear();
        PrCache.Clear();
    }

    /// <summary>Dispose and clear pooled HttpClients — call only when PAT changes.</summary>
    public static void ResetHttpPool()
    {
        foreach (var kv in HttpPool)
            kv.Value.Dispose();
        HttpPool.Clear();
    }

    // ------------------------------------------------------------------
    // WIQL
    // ------------------------------------------------------------------

    public async Task<List<int>> RunWiqlAsync(string wiql, int? top = null)
    {
        var url = $"{BaseUrl}/wit/wiql?api-version={_apiVersion}";
        if (top.HasValue) url += $"&$top={top.Value}";

        var content = JsonContent(new { query = wiql });
        var resp = await _http.PostAsync(url, content);
        ThrowIfPatScopeError(resp, url);
        resp.EnsureSuccessStatusCode();

        var doc = await ParseJsonAsync(resp);
        var ids = new List<int>();
        if (doc.RootElement.TryGetProperty("workItems", out var items))
        {
            foreach (var item in items.EnumerateArray())
                if (item.TryGetProperty("id", out var id))
                    ids.Add(id.GetInt32());
        }
        return ids;
    }

    // ------------------------------------------------------------------
    // Work Items
    // ------------------------------------------------------------------

    public async Task<List<JsonElement>> GetWorkItemsAsync(List<int> ids, int batchSize = 200,
        List<string>? fields = null, string expand = "Relations")
    {
        if (ids.Count == 0) return [];

        var cacheKeyBase = $"{BaseUrl}|{expand}|{(fields != null ? string.Join(",", fields.OrderBy(f => f)) : "")}";

        var cached = new List<JsonElement>();
        var uncached = new List<int>();
        var now = DateTime.UtcNow.Ticks;
        foreach (var id in ids)
        {
            var key = $"{cacheKeyBase}|{id}";
            if (WorkItemsCache.TryGetValue(key, out var entry) && now - entry.Ticks < WorkItemsTtlTicks)
                cached.Add(entry.Data);
            else
                uncached.Add(id);
        }

        if (uncached.Count == 0) return cached;

        var url = $"{BaseUrl}/wit/workitemsbatch?api-version={_apiVersion}";
        var chunks = uncached.Chunk(batchSize);

        var newItems = new ConcurrentBag<JsonElement>();
        var semaphore = new SemaphoreSlim(8);
        var tasks = chunks.Select(async chunk =>
        {
            await semaphore.WaitAsync();
            try
            {
                var payload = new Dictionary<string, object>();
                payload["ids"] = chunk;
                if (fields != null)
                    payload["fields"] = fields;
                else
                    payload["$expand"] = expand;

                var resp = await _http.PostAsync(url, JsonContent(payload));
                ThrowIfPatScopeError(resp, url);
                if (!resp.IsSuccessStatusCode) return;

                var doc = await ParseJsonAsync(resp);
                if (doc.RootElement.TryGetProperty("value", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var clone = item.Clone();
                        newItems.Add(clone);

                        if (item.TryGetProperty("id", out var idProp))
                        {
                            var key = $"{cacheKeyBase}|{idProp.GetInt32()}";
                            WorkItemsCache[key] = (DateTime.UtcNow.Ticks, clone);
                        }
                    }
                }
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);

        cached.AddRange(newItems);
        return cached;
    }

    public async Task<List<JsonElement>> GetWorkItemsOrgAsync(List<int> ids, int batchSize = 200, List<string>? fields = null)
    {
        if (ids.Count == 0) return [];

        var url = $"https://dev.azure.com/{_organization}/_apis/wit/workitemsbatch?api-version={_apiVersion}";
        var chunks = ids.Chunk(batchSize);

        var result = new ConcurrentBag<JsonElement>();
        var semaphore = new SemaphoreSlim(8);
        var tasks = chunks.Select(async chunk =>
        {
            await semaphore.WaitAsync();
            try
            {
                var payload = new Dictionary<string, object> { ["ids"] = chunk };
                if (fields != null) payload["fields"] = fields;

                var resp = await _http.PostAsync(url, JsonContent(payload));
                ThrowIfPatScopeError(resp, url);
                if (!resp.IsSuccessStatusCode) return;

                var doc = await ParseJsonAsync(resp);
                if (doc.RootElement.TryGetProperty("value", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                        result.Add(item.Clone());
                }
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);

        return result.ToList();
    }

    public async Task<List<string>> GetCommentsAsync(int itemId)
    {
        var url = $"{BaseUrl}/wit/workitems/{itemId}/comments?api-version={_apiVersion}-preview";
        var resp = await _http.GetAsync(url);
        ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return [];

        var doc = await ParseJsonAsync(resp);
        var comments = new List<string>();
        if (doc.RootElement.TryGetProperty("comments", out var arr))
        {
            foreach (var c in arr.EnumerateArray())
                if (c.TryGetProperty("text", out var text))
                    comments.Add(text.GetString() ?? "");
        }
        return comments;
    }

    public async Task<Dictionary<int, List<string>>> GetCommentsBatchAsync(List<int> itemIds)
    {
        if (itemIds.Count == 0) return [];
        var result = new ConcurrentDictionary<int, List<string>>();
        var semaphore = new SemaphoreSlim(8);
        var tasks = itemIds.Select(async id =>
        {
            await semaphore.WaitAsync();
            try { result[id] = await GetCommentsAsync(id); }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);
        return new Dictionary<int, List<string>>(result);
    }

    // ------------------------------------------------------------------
    // Pull Requests
    // ------------------------------------------------------------------

    public async Task<List<JsonElement>> GetActivePullRequestsAsync(string repository = "")
    {
        var cacheKey = $"{BaseUrl}|{(repository ?? "").Trim().ToLowerInvariant()}";
        if (PrCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow.Ticks - cached.Ticks < PrCacheTtlTicks)
            return cached.Data;

        var url = $"{BaseUrl}/git/pullrequests?searchCriteria.status=active&api-version={_apiVersion}";
        var allPrs = new List<JsonElement>();
        var skip = 0;

        while (true)
        {
            var pagedUrl = $"{url}&$skip={skip}&$top=100";
            var resp = await _http.GetAsync(pagedUrl);
            ThrowIfPatScopeError(resp, pagedUrl);
            resp.EnsureSuccessStatusCode();
            var doc = await ParseJsonAsync(resp);

            var batch = new List<JsonElement>();
            if (doc.RootElement.TryGetProperty("value", out var items))
                foreach (var item in items.EnumerateArray())
                    batch.Add(item.Clone());

            if (batch.Count == 0) break;
            allPrs.AddRange(batch);
            if (batch.Count < 100) break;
            skip += batch.Count;
        }

        if (!string.IsNullOrWhiteSpace(repository))
        {
            var repoLower = repository.Trim().ToLowerInvariant();
            allPrs = allPrs.Where(pr =>
            {
                if (pr.TryGetProperty("repository", out var repo) &&
                    repo.TryGetProperty("name", out var name))
                    return (name.GetString() ?? "").ToLowerInvariant() == repoLower;
                return false;
            }).ToList();
        }

        PrCache[cacheKey] = (DateTime.UtcNow.Ticks, allPrs);
        return allPrs;
    }

    public async Task<JsonElement?> GetPullRequestAsync(int prId)
    {
        var url = $"{BaseUrl}/git/pullrequests/{prId}?api-version={_apiVersion}";
        var resp = await _http.GetAsync(url);
        ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return null;
        return (await ParseJsonAsync(resp)).RootElement.Clone();
    }

    public async Task<Dictionary<int, JsonElement>> GetPullRequestsBatchAsync(List<int> prIds)
    {
        if (prIds.Count == 0) return [];
        var result = new ConcurrentDictionary<int, JsonElement>();
        var semaphore = new SemaphoreSlim(8);
        var tasks = prIds.Select(async pid =>
        {
            await semaphore.WaitAsync();
            try
            {
                var pr = await GetPullRequestAsync(pid);
                if (pr.HasValue) result[pid] = pr.Value;
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);
        return new Dictionary<int, JsonElement>(result);
    }

    public async Task<List<JsonElement>> GetPullRequestThreadsAsync(string repositoryId, int prId)
    {
        var url = $"{BaseUrl}/git/repositories/{repositoryId}/pullrequests/{prId}/threads?api-version={_apiVersion}";
        var resp = await _http.GetAsync(url);
        ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return [];

        var doc = await ParseJsonAsync(resp);
        var threads = new List<JsonElement>();
        if (doc.RootElement.TryGetProperty("value", out var items))
            foreach (var item in items.EnumerateArray())
                threads.Add(item.Clone());
        return threads;
    }

    public async Task<Dictionary<int, List<JsonElement>>> GetPullRequestThreadsBatchAsync(List<JsonElement> prs)
    {
        if (prs.Count == 0) return [];
        var result = new ConcurrentDictionary<int, List<JsonElement>>();
        var semaphore = new SemaphoreSlim(8);
        var tasks = prs.Select(async pr =>
        {
            await semaphore.WaitAsync();
            try
            {
                var prId = pr.GetProperty("pullRequestId").GetInt32();
                var repoId = "";
                if (pr.TryGetProperty("repository", out var repo) && repo.TryGetProperty("id", out var id))
                    repoId = id.GetString() ?? "";
                result[prId] = await GetPullRequestThreadsAsync(repoId, prId);
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);
        return new Dictionary<int, List<JsonElement>>(result);
    }

    // ------------------------------------------------------------------
    // Tags
    // ------------------------------------------------------------------

    public async Task<List<string>> GetTagsAsync()
    {
        var url = $"{BaseUrl}/wit/tags?api-version={_apiVersion}";
        var resp = await _http.GetAsync(url);
        ThrowIfPatScopeError(resp, url);
        resp.EnsureSuccessStatusCode();
        var doc = await ParseJsonAsync(resp);
        var tags = new List<string>();
        if (doc.RootElement.TryGetProperty("value", out var items))
            foreach (var item in items.EnumerateArray())
                if (item.TryGetProperty("name", out var name) && !string.IsNullOrEmpty(name.GetString()))
                    tags.Add(name.GetString()!);
        return tags;
    }

    public async Task<bool> DeleteTagAsync(string tagName)
    {
        // Find tag ID first
        var url = $"{BaseUrl}/wit/tags?api-version={_apiVersion}";
        var resp = await _http.GetAsync(url);
        ThrowIfPatScopeError(resp, url);
        resp.EnsureSuccessStatusCode();
        var doc = await ParseJsonAsync(resp);

        string? tagId = null;
        if (doc.RootElement.TryGetProperty("value", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var n) && n.GetString() == tagName &&
                    item.TryGetProperty("id", out var id))
                {
                    tagId = id.GetString();
                    break;
                }
            }
        }

        if (tagId is null) return false;

        var delUrl = $"{BaseUrl}/wit/tags/{tagId}?api-version={_apiVersion}";
        var delResp = await _http.DeleteAsync(delUrl);
        if ((int)delResp.StatusCode == 401)
            throw new UnauthorizedAccessException(
                "PAT lacks write permissions. Update your PAT to include the 'Work Items (Read & Write)' scope.");
        return (int)delResp.StatusCode is 200 or 204;
    }

    /// <summary>Update the System.Tags field of a single work item using JSON Patch.</summary>
    public async Task UpdateWorkItemTagsAsync(int workItemId, string newTags)
    {
        var url = $"{BaseUrl}/wit/workitems/{workItemId}?api-version={_apiVersion}";
        var patch = new[] { new { op = "replace", path = "/fields/System.Tags", value = newTags } };
        var content = new StringContent(
            JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json-patch+json");
        var resp = await _http.PatchAsync(url, content);
        if ((int)resp.StatusCode == 401)
            throw new UnauthorizedAccessException(
                "PAT lacks write permissions. Update your PAT to include the 'Work Items (Read & Write)' scope.");
        resp.EnsureSuccessStatusCode();
    }

    // ------------------------------------------------------------------
    // Iterations
    // ------------------------------------------------------------------

    public async Task<List<Dictionary<string, object?>>> GetIterationsAsync()
    {
        var cacheKey = $"{_organization}|{_project}|{_apiVersion}";
        if (IterationsCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow.Ticks - cached.Ticks < IterationsTtlTicks)
            return cached.Data;

        var url = $"{BaseUrl}/wit/classificationnodes/iterations?$depth=10&api-version={_apiVersion}";
        var resp = await _http.GetAsync(url);
        ThrowIfPatScopeError(resp, url);
        resp.EnsureSuccessStatusCode();
        var doc = await ParseJsonAsync(resp);

        var result = new List<Dictionary<string, object?>>();
        FlattenIterations(doc.RootElement, result);
        IterationsCache[cacheKey] = (DateTime.UtcNow.Ticks, result);
        return result;
    }

    private static void FlattenIterations(JsonElement node, List<Dictionary<string, object?>> results)
    {
        string? start = null, finish = null;
        if (node.TryGetProperty("attributes", out var attrs))
        {
            if (attrs.TryGetProperty("startDate", out var s)) start = s.GetString();
            if (attrs.TryGetProperty("finishDate", out var f)) finish = f.GetString();
        }

        var rawPath = node.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";

        if (start is not null && finish is not null)
        {
            // Convert path: strip leading backslash and "Iteration" segment
            var parts = rawPath.Trim('\\').Split('\\');
            if (parts.Length >= 2 && parts[1].Equals("Iteration", StringComparison.OrdinalIgnoreCase))
                parts = [parts[0], .. parts[2..]];
            var iterationPath = string.Join("\\", parts);

            results.Add(new Dictionary<string, object?>
            {
                ["path"] = iterationPath,
                ["startDate"] = start,
                ["finishDate"] = finish,
            });
        }

        if (node.TryGetProperty("children", out var children))
            foreach (var child in children.EnumerateArray())
                FlattenIterations(child, results);
    }

    // ------------------------------------------------------------------
    // HTTP helpers (async)
    // ------------------------------------------------------------------

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage resp) =>
        await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());

    // ------------------------------------------------------------------
    // PAT scope diagnostics
    // ------------------------------------------------------------------

    /// <summary>
    /// Maps Azure DevOps API path segments to the PAT scopes required.
    /// When a 401/403 is returned, this helps users know what to enable.
    /// </summary>
    internal static string GetRequiredPatScope(string url)
    {
        if (string.IsNullOrEmpty(url)) return "Full access or appropriate scopes";

        if (url.Contains("/_apis/wit/wiql") || url.Contains("/_apis/wit/workitemsbatch") ||
            url.Contains("/_apis/wit/workitems"))
            return "Work Items (Read) — scope 'vso.work'";

        if (url.Contains("/_apis/wit/tags"))
            return "Work Items (Read & Write) — scope 'vso.work_write'";

        if (url.Contains("/_apis/git/pullrequests") || url.Contains("/_apis/git/repositories"))
            return "Code (Read) — scope 'vso.code'";

        if (url.Contains("/_apis/work/teamsettings"))
            return "Work Items (Read) — scope 'vso.work'";

        if (url.Contains("/teams") && url.Contains("/members"))
            return "Project and Team (Read) — scope 'vso.project'";

        if (url.Contains("/_apis/projects") || url.Contains("/teams"))
            return "Project and Team (Read) — scope 'vso.project'";

        if (url.Contains("/_apis/wit/classificationnodes"))
            return "Work Items (Read) — scope 'vso.work'";

        if (url.Contains("/_apis/identities") || url.Contains("vssps.dev.azure.com"))
            return "Identity (Read) — scope 'vso.identity'";

        if (url.Contains("/_apis/security") || url.Contains("/_apis/accesscontrollists"))
            return "Security (Manage) — scope 'vso.security_manage'";

        if (url.Contains("/_apis/wiki"))
            return "Wiki (Read) — scope 'vso.wiki'";

        if (url.Contains("/_apis/graph"))
            return "Graph (Read) — scope 'vso.graph'";

        return "Full access or appropriate scopes";
    }

    /// <summary>
    /// Checks an HTTP response for 401/403 and throws a descriptive exception.
    /// Call this before silent-return or EnsureSuccessStatusCode to give PAT hints.
    /// </summary>
    internal static void ThrowIfPatScopeError(HttpResponseMessage resp, string url)
    {
        var code = (int)resp.StatusCode;
        if (code is 401 or 403)
        {
            var scope = GetRequiredPatScope(url);
            var status = code == 401 ? "Unauthorized (401)" : "Forbidden (403)";
            throw new AzureDevOpsPatScopeException(
                $"{status}: Your PAT lacks the required permissions. " +
                $"Ensure your PAT includes: {scope}.");
        }
    }
}

/// <summary>
/// Thrown when an Azure DevOps API call fails due to insufficient PAT permissions.
/// Contains a user-friendly message specifying which PAT scope is needed.
/// </summary>
public class AzureDevOpsPatScopeException : Exception
{
    public AzureDevOpsPatScopeException(string message) : base(message) { }
    public AzureDevOpsPatScopeException(string message, Exception inner) : base(message, inner) { }
}
