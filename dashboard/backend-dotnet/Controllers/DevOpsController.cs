using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashboardApi.Models;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/devops")]
public partial class DevOpsController(ConfigStore configStore) : ControllerBase
{
    private static readonly int Timeout = int.Parse(
        Environment.GetEnvironmentVariable("AZDO_TIMEOUT") ?? "30");

    private static readonly long CacheTtlTicks = TimeSpan.FromMinutes(5).Ticks;
    private static readonly ConcurrentDictionary<string, (long Ticks, object Data)> _cache = new();

    private static T? GetCached<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out var entry) && DateTime.UtcNow.Ticks - entry.Ticks < CacheTtlTicks)
            return entry.Data as T;
        return null;
    }

    private static void SetCached(string key, object data)
    {
        _cache[key] = (DateTime.UtcNow.Ticks, data);
    }

    [GeneratedRegex(@"^[a-zA-Z0-9 .\-_êéèëàáâäöüïîôçñ]{1,256}$")]
    private static partial Regex SafeNameRegex();

    private static string ValidateName(string value, string label)
    {
        if (!SafeNameRegex().IsMatch(value))
            throw new BadHttpRequestException($"Invalid {label}.");
        return value;
    }

    private static Dictionary<string, string> AuthHeader(string pat)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{pat}"));
        return new() { ["Authorization"] = $"Basic {token}" };
    }

    [HttpGet("organizations")]
    public List<OrgInfo> ListKnownOrganizations()
    {
        var cfg = configStore.LoadConfig();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orgs = new List<OrgInfo>();

        foreach (var p in cfg.Projects)
        {
            if (!string.IsNullOrEmpty(p.Organization) && seen.Add(p.Organization))
                orgs.Add(new OrgInfo(p.Organization));
        }

        orgs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return orgs;
    }

    [HttpGet("organizations/{org}/projects")]
    public async Task<IActionResult> ListOrgProjects(string org, CancellationToken cancellationToken = default)
    {
        org = ValidateName(org, "organization name");
        var cacheKey = $"projects|{org}";
        var cached = GetCached<List<ProjectInfo>>(cacheKey);
        if (cached is not null) return Ok(cached);

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var url = $"https://dev.azure.com/{org}/_apis/projects?api-version=7.1&$top=500";
        var http = HttpClientPool.Get(pat, Timeout);
        var auth = AuthHeader(pat);

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var h in auth) req.Headers.TryAddWithoutValidation(h.Key, h.Value);

        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req); AzureDevOpsClient.ThrowIfPatScopeError(resp, url); resp.EnsureSuccessStatusCode(); }
        catch (AzureDevOpsPatScopeException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch { return StatusCode(502, new { detail = "Failed to list projects from Azure DevOps. Ensure your PAT includes: Project and Team (Read) — scope 'vso.project'." }); }

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var projects = new List<ProjectInfo>();
        if (doc.RootElement.TryGetProperty("value", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? "";
                var id = item.GetProperty("id").GetString() ?? "";
                projects.Add(new ProjectInfo(name, id));
            }
        }
        projects.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        SetCached(cacheKey, projects);
        return Ok(projects);
    }

    [HttpGet("organizations/{org}/projects/{project}/areas")]
    public async Task<IActionResult> ListProjectAreas(string org, string project, CancellationToken cancellationToken = default)
    {
        org = ValidateName(org, "organization name");
        project = ValidateName(project, "project name");
        var cacheKey = $"areas|{org}|{project}";
        var cached = GetCached<List<AreaInfo>>(cacheKey);
        if (cached is not null) return Ok(cached);

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var url = $"https://dev.azure.com/{org}/{project}/_apis/wit/classificationnodes/Areas?$depth=10&api-version=7.1";
        var http = HttpClientPool.Get(pat, Timeout);
        var auth = AuthHeader(pat);

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var h in auth) req.Headers.TryAddWithoutValidation(h.Key, h.Value);

        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req); AzureDevOpsClient.ThrowIfPatScopeError(resp, url); resp.EnsureSuccessStatusCode(); }
        catch (AzureDevOpsPatScopeException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch { return StatusCode(502, new { detail = "Failed to list area paths from Azure DevOps. Ensure your PAT includes: Work Items (Read) — scope 'vso.work'." }); }

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var areas = new List<AreaInfo>();

        void Walk(JsonElement node)
        {
            var rawPath = node.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
            var clean = rawPath.TrimStart('\\');
            var parts = clean.Split('\\').ToList();
            if (parts.Count >= 2 && parts[1] == "Area")
                parts.RemoveAt(1);
            clean = string.Join("\\", parts);
            if (!string.IsNullOrEmpty(clean))
                areas.Add(new AreaInfo(clean));
            if (node.TryGetProperty("children", out var children))
                foreach (var child in children.EnumerateArray())
                    Walk(child);
        }

        Walk(doc.RootElement);
        SetCached(cacheKey, areas);
        return Ok(areas);
    }

    [HttpGet("organizations/{org}/projects/{project}/teams")]
    public async Task<IActionResult> ListProjectTeams(string org, string project, CancellationToken cancellationToken = default)
    {
        org = ValidateName(org, "organization name");
        project = ValidateName(project, "project name");
        var cacheKey = $"teams|{org}|{project}";
        var cached = GetCached<List<TeamInfo>>(cacheKey);
        if (cached is not null) return Ok(cached);

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/_apis/projects/{Uri.EscapeDataString(project)}/teams?api-version=7.1-preview.1&$top=300";
        var http = HttpClientPool.Get(pat, int.Parse(Environment.GetEnvironmentVariable("AZDO_TIMEOUT") ?? "30"));
        var auth = AuthHeader(pat);

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var h in auth) req.Headers.TryAddWithoutValidation(h.Key, h.Value);

        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req); AzureDevOpsClient.ThrowIfPatScopeError(resp, url); resp.EnsureSuccessStatusCode(); }
        catch (AzureDevOpsPatScopeException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch { return StatusCode(502, new { detail = "Failed to list teams from Azure DevOps. Ensure your PAT includes: Project and Team (Read) — scope 'vso.project'." }); }

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var teams = new List<TeamInfo>();
        if (doc.RootElement.TryGetProperty("value", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? "";
                var id = item.TryGetProperty("id", out var tid) ? tid.GetString() ?? "" : "";
                teams.Add(new TeamInfo(name, id));
            }
        }
        teams.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        SetCached(cacheKey, teams);
        return Ok(teams);
    }

    [HttpGet("organizations/{org}/projects/{project}/repos")]
    public async Task<IActionResult> ListProjectRepos(string org, string project, CancellationToken cancellationToken = default)
    {
        org = ValidateName(org, "organization name");
        project = ValidateName(project, "project name");
        var cacheKey = $"repos|{org}|{project}";
        var cached = GetCached<List<RepoInfo>>(cacheKey);
        if (cached is not null) return Ok(cached);

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(project)}/_apis/git/repositories?api-version=7.1";
        var http = HttpClientPool.Get(pat, Timeout);
        var auth = AuthHeader(pat);

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var h in auth) req.Headers.TryAddWithoutValidation(h.Key, h.Value);

        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req); AzureDevOpsClient.ThrowIfPatScopeError(resp, url); resp.EnsureSuccessStatusCode(); }
        catch (AzureDevOpsPatScopeException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch { return StatusCode(502, new { detail = "Failed to list repositories from Azure DevOps. Ensure your PAT includes: Code (Read) — scope 'vso.code'." }); }

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var repos = new List<RepoInfo>();
        if (doc.RootElement.TryGetProperty("value", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? "";
                var id = item.GetProperty("id").GetString() ?? "";
                repos.Add(new RepoInfo(name, id));
            }
        }
        repos.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        SetCached(cacheKey, repos);
        return Ok(repos);
    }

    [GeneratedRegex(@"^[\w .\.\-–—,;:()&+#@!êéèëàáâäöüïîôçñ]{1,256}$")]
    private static partial Regex SafeTagNameRegex();

    [HttpPost("tags/delete")]
    public async Task<IActionResult> DeleteTag([FromBody] DeleteTagRequest body, CancellationToken cancellationToken = default)
    {
        var org = ValidateName(body.Organization, "organization name");
        var project = ValidateName(body.Project, "project name");
        if (string.IsNullOrWhiteSpace(body.TagName) || !SafeTagNameRegex().IsMatch(body.TagName))
            return BadRequest(new { detail = "Invalid tag name." });
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var client = new AzureDevOpsClient(org, project, pat);
        try
        {
            var ok = await client.DeleteTagAsync(body.TagName);
            if (!ok) return NotFound(new { detail = "Tag not found or could not be deleted." });
            return Ok(new { ok = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
    }
}
