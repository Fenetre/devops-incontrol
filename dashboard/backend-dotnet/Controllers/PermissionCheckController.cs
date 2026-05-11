using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using DashboardApi.Models;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/permission-check")]
public class PermissionCheckController(ConfigStore configStore) : ControllerBase
{
    // 5-minute cache keyed by projectId
    private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, PermissionMatrixResponse Data)> Cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const int MaxConcurrentRepoPermissionChecks = 8;

    // Deprecated / internal-only namespaces to skip
    private static readonly HashSet<string> SkipNamespaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "CrossProjectWidgetView", "DataProvider", "Favorites", "Graph", "Identity2",
        "IdentityPicker", "Job", "Location", "ProjectAnalysisLanguageMetrics", "Proxy",
        "Publish", "Registry", "Security", "ServicingOrchestration", "SettingEntries",
        "Social", "StrongBox", "TeamLabSecurity", "TestManagement", "VersionControlItems2",
        "ViewActivityPaneSecurity", "WebPlatform", "WorkItemsHub", "WorkItemTracking",
        "WorkItemTrackingConfiguration",
        // Internal-only namespaces that don't surface useful project permissions
        "AccountAdminSecurity", "BlobStoreBlobPrivileges", "BoardsExternalIntegration",
        "Chat", "EventPublish", "EventSubscriber", "EventSubscription",
        "Licensing", "PermissionLevel", "OrganizationLevelData",
        "PipelineCachePrivileges", "SearchSecurity", "UtilizationPermissions",
        "WorkItemTrackingAdministration", "WorkItemTrackingProvision",
    };

    // Internal-only namespace IDs to skip (for namespaces that share a name with
    // a useful namespace, e.g. the internal "ReleaseManagement" UI namespace).
    private static readonly HashSet<string> SkipNamespaceIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "7c7d32f7-0e86-4cd6-892e-b35dbba870bd", // Internal ReleaseManagement (ViewTaskEditor, ViewCDWorkflowEditor, etc.)
    };

    // Known project-scoped token formats per namespace name
    private static readonly Dictionary<string, string> TokenFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Project"] = "$PROJECT:vstfs:///Classification/TeamProject/{0}",
        ["Git Repositories"] = "repoV2/{0}",
        ["Build"] = "{0}",
        ["ReleaseManagement"] = "{0}",
        ["Tagging"] = "/{0}",
        ["AnalyticsViews"] = "$/Shared/{0}",
        ["DashboardsPrivileges"] = "$/{0}",
        ["WorkItemQueryFolders"] = "/{0}",
        ["Plan"] = "Plan/{0}",
        ["MetaTask"] = "{0}",
        ["DistributedTask"] = "{0}",
        ["Environment"] = "{0}",
        ["Library"] = "{0}",
        ["ServiceEndpoints"] = "{0}",
        ["CSS"] = "vstfs:///Classification/Node/{0}",
        ["Iteration"] = "vstfs:///Classification/Node/{0}",
        ["VersionControlItems"] = "$/{0}",
        ["Boards"] = "{0}",
        ["Analytics"] = "$/{0}",
    };

    // ---------------------------------------------------------------
    // GET /api/permission-check/projects — list configured projects
    // ---------------------------------------------------------------
    [HttpGet("projects")]
    public List<object> ListProjects()
    {
        return configStore.ListProjects()
            .Select(p => new { p.Id, p.Organization, p.Project })
            .Cast<object>()
            .ToList();
    }

    // ---------------------------------------------------------------
    // GET /api/permission-check/{projectId}?force=false
    // ---------------------------------------------------------------
    [HttpGet("{projectId}")]
    public async Task<IActionResult> GetPermissionMatrix(string projectId, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        // Check cache
        if (!force && Cache.TryGetValue(projectId, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
        {
            return Ok(cached.Data);
        }

        try
        {
            var http = BuildClient(pat);
            var orgUrl = $"https://dev.azure.com/{project.Organization}";
            var vsspsUrl = $"https://vssps.dev.azure.com/{project.Organization}";

            // Step 1: Resolve project GUID
            var projectGuid = await GetProjectGuid(http, orgUrl, project.Project);
            if (projectGuid is null)
                return StatusCode(502, new { detail = "Failed to resolve project." });

            // Step 2: Get all teams + deduplicated members
            var (teams, members) = await GetAllTeamMembers(http, orgUrl, project.Project);
            if (members.Count == 0)
                return Ok(new PermissionMatrixResponse
                {
                    Teams = teams.Select(t => new PermissionTeam { Id = t.Id, Name = t.Name }).ToList(),
                    FetchedAt = DateTime.UtcNow.ToString("o"),
                });

            // Step 3: Resolve identity descriptors + classification nodes in parallel
            var identityTask = ResolveIdentities(http, vsspsUrl, members);
            var rootAreaTask = GetRootClassificationNodeId(http, orgUrl, project.Project, "Areas");
            var rootIterationTask = GetRootClassificationNodeId(http, orgUrl, project.Project, "Iterations");
            await Task.WhenAll(identityTask, rootAreaTask, rootIterationTask);

            var identityMap = await identityTask;
            var rootAreaNodeId = await rootAreaTask;
            var rootIterationNodeId = await rootIterationTask;

            // Step 4: Get security namespaces + ACLs
            var (categories, acls) = await GetSecurityData(http, orgUrl, projectGuid, rootAreaNodeId, rootIterationNodeId);

            // Step 5: Compute effective permissions
            var matrix = ComputeEffectivePermissions(members, identityMap, categories, acls);

            var response = new PermissionMatrixResponse
            {
                Teams = teams.Select(t => new PermissionTeam { Id = t.Id, Name = t.Name }).ToList(),
                Members = members.Values.Select(m => new PermissionMember
                {
                    Id = m.Id,
                    DisplayName = m.DisplayName,
                    UniqueName = m.UniqueName,
                    ImageUrl = m.ImageUrl,
                    TeamNames = m.TeamNames.ToList(),
                }).OrderBy(m => m.DisplayName).ToList(),
                Categories = categories,
                Matrix = matrix,
                FetchedAt = DateTime.UtcNow.ToString("o"),
            };

            Cache[projectId] = (DateTime.UtcNow, response);
            return Ok(response);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return StatusCode(403, new { detail = "PAT lacks required permissions. Ensure your PAT has 'Identity (Read)' and 'Security (Manage)' scopes." });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { detail = $"Failed to fetch permissions: {ex.Message}" });
        }
    }

    // ===================================================================
    // Private helpers
    // ===================================================================

    private (string? Pat, ProjectConfig? Project) Resolve(string projectId)
    {
        var pat = SettingsController.GetPat(configStore);
        var project = configStore.GetProject(projectId);
        return (pat, project);
    }

    private static HttpClient BuildClient(string pat)
        => HttpClientPool.Get(pat, 60);

    // --- Step 1: Project GUID ---
    private static async Task<string?> GetProjectGuid(HttpClient http, string orgUrl, string project)
    {
        var url = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(project)}?api-version=7.1";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return null;

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    // --- Step 1b: Root classification node IDs (Area/Iteration) ---
    private static async Task<string?> GetRootClassificationNodeId(
        HttpClient http, string orgUrl, string project, string structureType)
    {
        // structureType: "Areas" or "Iterations"
        var url = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/classificationnodes/{structureType}?$depth=0&api-version=7.1";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return null;

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        return doc.RootElement.TryGetProperty("identifier", out var identifier)
            ? identifier.GetString()
            : null;
    }

    // --- Step 2: All teams + deduplicated members ---
    private record TeamInfo(string Id, string Name);
    private class MemberInfo
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string UniqueName { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public HashSet<string> TeamNames { get; set; } = [];
        public string? Descriptor { get; set; }
        public HashSet<string> GroupDescriptors { get; set; } = [];
    }

    private static async Task<(List<TeamInfo> Teams, Dictionary<string, MemberInfo> Members)>
        GetAllTeamMembers(HttpClient http, string orgUrl, string project)
    {
        // List all teams
        var teamsUrl = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(project)}/teams?api-version=7.1-preview.1&$top=300";
        var teamsResp = await http.GetAsync(teamsUrl);
        AzureDevOpsClient.ThrowIfPatScopeError(teamsResp, teamsUrl);
        if (!teamsResp.IsSuccessStatusCode) return ([], new());

        var teamsDoc = await JsonDocument.ParseAsync(await teamsResp.Content.ReadAsStreamAsync());
        var teams = new List<TeamInfo>();
        if (teamsDoc.RootElement.TryGetProperty("value", out var teamArr))
        {
            foreach (var t in teamArr.EnumerateArray())
            {
                var teamId = t.TryGetProperty("id", out var tid) ? tid.GetString() ?? "" : "";
                var teamName = t.TryGetProperty("name", out var tn) ? tn.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(teamId))
                    teams.Add(new TeamInfo(teamId, teamName));
            }
        }

        // Get members per team in parallel, deduplicate
        var members = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);
        var teamThrottler = new SemaphoreSlim(8);
        var teamMemberResults = new ConcurrentBag<(string TeamName, List<JsonElement> Members)>();
        var teamMemberTasks = teams.Select(async team =>
        {
            await teamThrottler.WaitAsync();
            try
            {
                var membersUrl = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(project)}/teams/{Uri.EscapeDataString(team.Id)}/members?api-version=7.1-preview.1&$top=300";
                var membersResp = await http.GetAsync(membersUrl);
                AzureDevOpsClient.ThrowIfPatScopeError(membersResp, membersUrl);
                if (!membersResp.IsSuccessStatusCode) return;

                var membersDoc = await JsonDocument.ParseAsync(await membersResp.Content.ReadAsStreamAsync());
                if (!membersDoc.RootElement.TryGetProperty("value", out var memberArr)) return;

                var list = new List<JsonElement>();
                foreach (var m in memberArr.EnumerateArray())
                    list.Add(m.Clone());
                teamMemberResults.Add((team.Name, list));
            }
            finally { teamThrottler.Release(); }
        });
        await Task.WhenAll(teamMemberTasks);

        foreach (var (teamName, memberList) in teamMemberResults)
        {
            foreach (var m in memberList)
            {
                var identity = m.TryGetProperty("identity", out var nested) ? nested : m;

                if (identity.TryGetProperty("isContainer", out var isContainer) && isContainer.ValueKind == JsonValueKind.True)
                    continue;

                var id = identity.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(id)) continue;

                if (!members.TryGetValue(id, out var existing))
                {
                    existing = new MemberInfo
                    {
                        Id = id,
                        DisplayName = identity.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "",
                        UniqueName = identity.TryGetProperty("uniqueName", out var un) ? un.GetString() ?? "" : "",
                        ImageUrl = identity.TryGetProperty("imageUrl", out var img) ? img.GetString() ?? "" : "",
                    };
                    members[id] = existing;
                }
                existing.TeamNames.Add(teamName);
            }
        }

        return (teams, members);
    }

    // --- Step 3: Identity descriptors + group memberships ---
    private static async Task<Dictionary<string, MemberInfo>> ResolveIdentities(
        HttpClient http, string vsspsUrl, Dictionary<string, MemberInfo> members)
    {
        if (members.Count == 0) return members;

        // Batch in groups of 50
        var allIds = members.Keys.ToList();
        for (var i = 0; i < allIds.Count; i += 50)
        {
            var batch = allIds.Skip(i).Take(50);
            var idsParam = string.Join(",", batch);
            // Identity API lives on vssps.dev.azure.com, not dev.azure.com
            var url = $"{vsspsUrl}/_apis/identities?identityIds={idsParam}&queryMembership=Expanded&api-version=7.1";

            var resp = await http.GetAsync(url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode) continue;

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("value", out var arr)) continue;

            foreach (var identity in arr.EnumerateArray())
            {
                var id = identity.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "";
                if (!members.TryGetValue(id, out var member)) continue;

                // Get descriptor
                if (identity.TryGetProperty("descriptor", out var desc))
                    member.Descriptor = desc.GetString();

                // Get group memberships (memberOf)
                if (identity.TryGetProperty("memberOf", out var memberOf))
                {
                    foreach (var group in memberOf.EnumerateArray())
                    {
                        var groupDesc = group.ValueKind == JsonValueKind.String
                            ? group.GetString()
                            : group.TryGetProperty("descriptor", out var gd) ? gd.GetString() : null;
                        if (!string.IsNullOrEmpty(groupDesc))
                            member.GroupDescriptors.Add(groupDesc);
                    }
                }
            }
        }

        return members;
    }

    // --- Step 4: Security namespaces + ACLs ---
    private static async Task<(List<PermissionCategory> Categories, Dictionary<string, Dictionary<string, AceInfo>> Acls)>
        GetSecurityData(HttpClient http, string orgUrl, string projectGuid,
            string? rootAreaNodeId = null, string? rootIterationNodeId = null)
    {
        // Fetch all security namespaces
        var nsUrl = $"{orgUrl}/_apis/securitynamespaces?api-version=7.1";
        var nsResp = await http.GetAsync(nsUrl);
        AzureDevOpsClient.ThrowIfPatScopeError(nsResp, nsUrl);
        if (!nsResp.IsSuccessStatusCode) return ([], new());

        var nsDoc = await JsonDocument.ParseAsync(await nsResp.Content.ReadAsStreamAsync());
        var categories = new List<PermissionCategory>();
        // ACLs: namespaceId -> descriptor -> AceInfo (allow/deny bitmasks)
        var acls = new Dictionary<string, Dictionary<string, AceInfo>>(StringComparer.OrdinalIgnoreCase);

        if (!nsDoc.RootElement.TryGetProperty("value", out var nsArr)) return (categories, acls);

        foreach (var ns in nsArr.EnumerateArray())
        {
            var nsName = ns.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var nsId = ns.TryGetProperty("namespaceId", out var nid) ? nid.GetString() ?? "" : "";
            var displayName = ns.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? nsName : nsName;

            if (string.IsNullOrEmpty(nsId) || SkipNamespaces.Contains(nsName) || SkipNamespaceIds.Contains(nsId)) continue;

            // Parse actions
            var actions = new List<PermissionAction>();
            if (ns.TryGetProperty("actions", out var actArr))
            {
                foreach (var act in actArr.EnumerateArray())
                {
                    var actName = act.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                    var actDisplay = act.TryGetProperty("displayName", out var adn) ? adn.GetString() ?? actName : actName;
                    var bit = act.TryGetProperty("bit", out var b) ? b.GetInt32() : 0;
                    if (bit > 0)
                        actions.Add(new PermissionAction { Name = actName, DisplayName = actDisplay, Bit = bit });
                }
            }

            if (actions.Count == 0) continue;

            categories.Add(new PermissionCategory
            {
                NamespaceId = nsId,
                Name = nsName,
                DisplayName = displayName,
                Permissions = actions,
            });

            // Build token — use root node IDs for CSS/Iteration instead of project GUID
            var tokenId = nsName switch
            {
                "CSS" => rootAreaNodeId ?? projectGuid,
                "Iteration" => rootIterationNodeId ?? projectGuid,
                _ => projectGuid,
            };
            var token = BuildToken(nsName, tokenId);
            if (token is null) continue;

            // Use recurse=true only for area/iteration paths (hierarchical classification nodes).
            // Git Repositories, Build, ReleaseManagement use recurse=false so only project-level
            // permissions are shown — per-repo details are handled by the Repositories tab.
            var recurse = nsName is "CSS" or "Iteration" ? "true" : "false";
            var aclUrl = $"{orgUrl}/_apis/accesscontrollists/{nsId}?token={Uri.EscapeDataString(token)}&recurse={recurse}&includeExtendedInfo=true&api-version=7.1";
            var aclResp = await http.GetAsync(aclUrl);
            if (!aclResp.IsSuccessStatusCode) continue;

            var aclDoc = await JsonDocument.ParseAsync(await aclResp.Content.ReadAsStreamAsync());
            var nsAcls = new Dictionary<string, AceInfo>(StringComparer.OrdinalIgnoreCase);

            if (aclDoc.RootElement.TryGetProperty("value", out var aclArr))
            {
                foreach (var acl in aclArr.EnumerateArray())
                {
                    if (!acl.TryGetProperty("acesDictionary", out var aces)) continue;
                    foreach (var ace in aces.EnumerateObject())
                    {
                        var descriptor = ace.Name;
                        var allow = ace.Value.TryGetProperty("allow", out var a) ? a.GetInt32() : 0;
                        var deny = ace.Value.TryGetProperty("deny", out var d) ? d.GetInt32() : 0;

                        if (nsAcls.TryGetValue(descriptor, out var existing))
                        {
                            existing.Allow |= allow;
                            existing.Deny |= deny;
                        }
                        else
                        {
                            nsAcls[descriptor] = new AceInfo { Allow = allow, Deny = deny };
                        }
                    }
                }
            }

            acls[nsId] = nsAcls;
        }

        return (categories, acls);
    }

    private record AceInfo
    {
        public int Allow { get; set; }
        public int Deny { get; set; }
    }

    private static string? BuildToken(string nsName, string projectGuid)
    {
        if (TokenFormats.TryGetValue(nsName, out var fmt))
            return string.Format(fmt, projectGuid);
        // Fallback: use project GUID as token
        return projectGuid;
    }

    // --- Step 5: Compute effective permissions ---
    private static List<PermissionEntry> ComputeEffectivePermissions(
        Dictionary<string, MemberInfo> members,
        Dictionary<string, MemberInfo> identityMap, // same ref
        List<PermissionCategory> categories,
        Dictionary<string, Dictionary<string, AceInfo>> acls)
    {
        var matrix = new List<PermissionEntry>();

        foreach (var member in members.Values)
        {
            // Collect all descriptors: own + groups
            var descriptors = new List<string>();
            if (member.Descriptor is not null) descriptors.Add(member.Descriptor);
            descriptors.AddRange(member.GroupDescriptors);

            foreach (var cat in categories)
            {
                if (!acls.TryGetValue(cat.NamespaceId, out var nsAcls)) continue;

                foreach (var perm in cat.Permissions)
                {
                    var effective = "not_set";
                    var hasAllow = false;
                    var hasDeny = false;

                    foreach (var desc in descriptors)
                    {
                        if (!nsAcls.TryGetValue(desc, out var ace)) continue;
                        if ((ace.Deny & perm.Bit) != 0) hasDeny = true;
                        if ((ace.Allow & perm.Bit) != 0) hasAllow = true;
                    }

                    // Deny wins over allow
                    if (hasDeny) effective = "deny";
                    else if (hasAllow) effective = "allow";

                    matrix.Add(new PermissionEntry
                    {
                        MemberId = member.Id,
                        NamespaceId = cat.NamespaceId,
                        PermissionName = perm.Name,
                        Effective = effective,
                    });
                }
            }
        }

        return matrix;
    }

    // ===================================================================
    // Lightweight lists (repos + areas) — instant dropdown population
    // ===================================================================

    [HttpGet("{projectId}/repo-list")]
    public async Task<IActionResult> GetRepoList(string projectId, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        // If the full repo permissions are already cached, extract lists from there
        if (RepoCache.TryGetValue(projectId, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
        {
            return Ok(cached.Data.Repos.Select(r => new { id = r.RepoId, name = r.RepoName }));
        }

        try
        {
            var http = BuildClient(pat);
            var orgUrl = $"https://dev.azure.com/{project.Organization}";
            var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/git/repositories?api-version=7.1";
            var resp = await http.GetAsync(url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode) return StatusCode(502, new { detail = "Failed to list repositories. Ensure your PAT includes: Code (Read) — scope 'vso.code'." });

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var list = new List<object>();
            if (doc.RootElement.TryGetProperty("value", out var arr))
            {
                foreach (var r in arr.EnumerateArray())
                {
                    var id = r.TryGetProperty("id", out var rid) ? rid.GetString() ?? "" : "";
                    var name = r.TryGetProperty("name", out var rn) ? rn.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(id)) list.Add(new { id, name });
                }
            }
            list.Sort((a, b) => string.Compare(((dynamic)a).name, ((dynamic)b).name, StringComparison.OrdinalIgnoreCase));
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { detail = $"Failed: {ex.Message}" });
        }
    }

    [HttpGet("{projectId}/area-list")]
    public async Task<IActionResult> GetAreaList(string projectId, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        // If the full area permissions are already cached, extract lists from there
        if (AreaCache.TryGetValue(projectId, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
        {
            return Ok(cached.Data.Areas.Select(a => new { id = a.AreaId, name = a.AreaPath }));
        }

        try
        {
            var http = BuildClient(pat);
            var orgUrl = $"https://dev.azure.com/{project.Organization}";
            var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/wit/classificationnodes/Areas?$depth=10&api-version=7.1";
            var resp = await http.GetAsync(url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode) return StatusCode(502, new { detail = "Failed to list area paths. Ensure your PAT includes: Work Items (Read) — scope 'vso.work'." });

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var list = new List<object>();
            void Walk(JsonElement node)
            {
                var id = node.TryGetProperty("identifier", out var ident) ? ident.GetString() ?? "" : "";
                var rawPath = node.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                var clean = rawPath.TrimStart('\\');
                var parts = clean.Split('\\').ToList();
                if (parts.Count >= 2 && parts[1] == "Area") parts.RemoveAt(1);
                clean = string.Join("\\", parts);
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(clean))
                    list.Add(new { id, name = clean });
                if (node.TryGetProperty("children", out var children))
                    foreach (var child in children.EnumerateArray()) Walk(child);
            }
            Walk(doc.RootElement);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { detail = $"Failed: {ex.Message}" });
        }
    }

    // ===================================================================
    // Repo Permissions
    // ===================================================================

    private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, RepoPermissionResponse Data)> RepoCache = new();

    [HttpGet("{projectId}/repos")]
    public async Task<IActionResult> GetRepoPermissions(string projectId, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        if (!force && RepoCache.TryGetValue(projectId, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
            return Ok(cached.Data);

        try
        {
            var http = BuildClient(pat);
            var orgUrl = $"https://dev.azure.com/{project.Organization}";
            var vsspsUrl = $"https://vssps.dev.azure.com/{project.Organization}";

            var projectGuid = await GetProjectGuid(http, orgUrl, project.Project);
            if (projectGuid is null) return StatusCode(502, new { detail = "Failed to resolve project." });

            // Get teams + members + identity descriptors (reuse existing helpers)
            var (_, members) = await GetAllTeamMembers(http, orgUrl, project.Project);
            if (members.Count > 0)
                await ResolveIdentities(http, vsspsUrl, members);

            // Get repos
            var reposUrl = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/git/repositories?api-version=7.1";
            var reposResp = await http.GetAsync(reposUrl);
            AzureDevOpsClient.ThrowIfPatScopeError(reposResp, reposUrl);
            if (!reposResp.IsSuccessStatusCode)
                return StatusCode(502, new { detail = "Failed to list repositories. Ensure your PAT includes: Code (Read) — scope 'vso.code'." });

            var reposDoc = await JsonDocument.ParseAsync(await reposResp.Content.ReadAsStreamAsync());
            var repos = new List<RepoPermissionEntry>();

            // Get the Git Repositories namespace ID
            var gitNsId = await GetNamespaceId(http, orgUrl, "Git Repositories");

            if (reposDoc.RootElement.TryGetProperty("value", out var repoArr) && gitNsId is not null)
            {
                // Get Git Repositories namespace actions
                var gitActions = await GetNamespaceActions(http, orgUrl, gitNsId);

                // Fetch project-level Git ACLs (inherited by all repos)
                var projectToken = $"repoV2/{projectGuid}";
                var projectAclUrl = $"{orgUrl}/_apis/accesscontrollists/{gitNsId}?token={Uri.EscapeDataString(projectToken)}&recurse=false&includeExtendedInfo=true&api-version=7.1";
                var projectAclResp = await http.GetAsync(projectAclUrl);
                var projectAcls = new Dictionary<string, AceInfo>(StringComparer.OrdinalIgnoreCase);
                if (projectAclResp.IsSuccessStatusCode)
                {
                    var pDoc = await JsonDocument.ParseAsync(await projectAclResp.Content.ReadAsStreamAsync());
                    if (pDoc.RootElement.TryGetProperty("value", out var pArr))
                    {
                        foreach (var acl in pArr.EnumerateArray())
                        {
                            if (!acl.TryGetProperty("acesDictionary", out var aces)) continue;
                            foreach (var ace in aces.EnumerateObject())
                            {
                                var allow = ace.Value.TryGetProperty("allow", out var a) ? a.GetInt32() : 0;
                                var deny = ace.Value.TryGetProperty("deny", out var d) ? d.GetInt32() : 0;
                                if (projectAcls.TryGetValue(ace.Name, out var ex)) { ex.Allow |= allow; ex.Deny |= deny; }
                                else projectAcls[ace.Name] = new AceInfo { Allow = allow, Deny = deny };
                            }
                        }
                    }
                }

                var repoEntries = new ConcurrentBag<RepoPermissionEntry>();
                using var repoThrottler = new SemaphoreSlim(MaxConcurrentRepoPermissionChecks);

                var repoTasks = repoArr.EnumerateArray()
                    .Select(r => r.Clone())
                    .Select(async repo =>
                    {
                        await repoThrottler.WaitAsync();
                        try
                        {
                            var repoId = repo.TryGetProperty("id", out var rid) ? rid.GetString() ?? "" : "";
                            var repoName = repo.TryGetProperty("name", out var rn) ? rn.GetString() ?? "" : "";
                            if (string.IsNullOrEmpty(repoId)) return;

                            // Per-repo ACL token
                            var token = $"repoV2/{projectGuid}/{repoId}";
                            var aclUrl = $"{orgUrl}/_apis/accesscontrollists/{gitNsId}?token={Uri.EscapeDataString(token)}&recurse=false&includeExtendedInfo=true&api-version=7.1";
                            var aclResp = await http.GetAsync(aclUrl);

                            var repoAcls = new Dictionary<string, AceInfo>(StringComparer.OrdinalIgnoreCase);
                            if (aclResp.IsSuccessStatusCode)
                            {
                                var aclDoc = await JsonDocument.ParseAsync(await aclResp.Content.ReadAsStreamAsync());
                                if (aclDoc.RootElement.TryGetProperty("value", out var aclArr))
                                {
                                    foreach (var acl in aclArr.EnumerateArray())
                                    {
                                        if (!acl.TryGetProperty("acesDictionary", out var aces)) continue;
                                        foreach (var ace in aces.EnumerateObject())
                                        {
                                            var allow = ace.Value.TryGetProperty("allow", out var a) ? a.GetInt32() : 0;
                                            var deny = ace.Value.TryGetProperty("deny", out var d) ? d.GetInt32() : 0;
                                            if (repoAcls.TryGetValue(ace.Name, out var ex)) { ex.Allow |= allow; ex.Deny |= deny; }
                                            else repoAcls[ace.Name] = new AceInfo { Allow = allow, Deny = deny };
                                        }
                                    }
                                }
                            }

                            // Merge inherited project-level ACLs (repo-level overrides take precedence)
                            foreach (var (desc, ace) in projectAcls)
                            {
                                if (repoAcls.TryGetValue(desc, out var existing))
                                {
                                    existing.Allow |= ace.Allow;
                                    existing.Deny |= ace.Deny;
                                }
                                else
                                {
                                    repoAcls[desc] = new AceInfo { Allow = ace.Allow, Deny = ace.Deny };
                                }
                            }

                            // Build permission entries for this repo
                            var permEntries = new List<RepoPermAction>();
                            foreach (var action in gitActions)
                            {
                                var allowed = new List<string>();
                                var denied = new List<string>();
                                foreach (var member in members.Values)
                                {
                                    var descriptors = new List<string>();
                                    if (member.Descriptor is not null) descriptors.Add(member.Descriptor);
                                    descriptors.AddRange(member.GroupDescriptors);

                                    var hasAllow = false;
                                    var hasDeny = false;
                                    foreach (var desc in descriptors)
                                    {
                                        if (!repoAcls.TryGetValue(desc, out var ace)) continue;
                                        if ((ace.Deny & action.Bit) != 0) hasDeny = true;
                                        if ((ace.Allow & action.Bit) != 0) hasAllow = true;
                                    }

                                    if (hasDeny) denied.Add(member.DisplayName);
                                    else if (hasAllow) allowed.Add(member.DisplayName);
                                }
                                permEntries.Add(new RepoPermAction
                                {
                                    Name = action.Name,
                                    DisplayName = action.DisplayName,
                                    MembersAllowed = allowed.OrderBy(n => n).ToList(),
                                    MembersDenied = denied.OrderBy(n => n).ToList(),
                                });
                            }

                            repoEntries.Add(new RepoPermissionEntry
                            {
                                RepoId = repoId,
                                RepoName = repoName,
                                Permissions = permEntries,
                            });
                        }
                        finally
                        {
                            repoThrottler.Release();
                        }
                    });

                await Task.WhenAll(repoTasks);
                repos.AddRange(repoEntries);
            }

            repos.Sort((a, b) => string.Compare(a.RepoName, b.RepoName, StringComparison.OrdinalIgnoreCase));
            var response = new RepoPermissionResponse { Repos = repos, FetchedAt = DateTime.UtcNow.ToString("o") };
            RepoCache[projectId] = (DateTime.UtcNow, response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { detail = $"Failed to fetch repo permissions: {ex.Message}" });
        }
    }

    // ===================================================================
    // Area Permissions
    // ===================================================================

    private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, AreaPermissionResponse Data)> AreaCache = new();

    [HttpGet("{projectId}/areas")]
    public async Task<IActionResult> GetAreaPermissions(string projectId, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        if (!force && AreaCache.TryGetValue(projectId, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
            return Ok(cached.Data);

        try
        {
            var http = BuildClient(pat);
            var orgUrl = $"https://dev.azure.com/{project.Organization}";
            var vsspsUrl = $"https://vssps.dev.azure.com/{project.Organization}";

            var projectGuid = await GetProjectGuid(http, orgUrl, project.Project);
            if (projectGuid is null) return StatusCode(502, new { detail = "Failed to resolve project." });

            // Get teams + members + identity descriptors
            var (_, members) = await GetAllTeamMembers(http, orgUrl, project.Project);
            if (members.Count > 0)
                await ResolveIdentities(http, vsspsUrl, members);

            // Fetch area tree
            var areasUrl = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/wit/classificationnodes/Areas?$depth=10&api-version=7.1";
            var areasResp = await http.GetAsync(areasUrl);
            AzureDevOpsClient.ThrowIfPatScopeError(areasResp, areasUrl);
            if (!areasResp.IsSuccessStatusCode)
                return StatusCode(502, new { detail = "Failed to list area paths. Ensure your PAT includes: Work Items (Read) — scope 'vso.work'." });

            var areasDoc = await JsonDocument.ParseAsync(await areasResp.Content.ReadAsStreamAsync());

            // Get CSS namespace ID + actions
            var cssNsId = await GetNamespaceId(http, orgUrl, "CSS");
            if (cssNsId is null)
                return StatusCode(502, new { detail = "Failed to find CSS security namespace." });

            var cssActions = await GetNamespaceActions(http, orgUrl, cssNsId);

            // Walk tree to collect area nodes in order (root first, children after)
            var areaNodes = new List<(string Id, string Path, string? ParentId)>();
            void WalkAreas(JsonElement node, string? parentId)
            {
                var id = node.TryGetProperty("identifier", out var ident) ? ident.GetString() ?? "" : "";
                var rawPath = node.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                // Clean path: strip leading backslash, remove "Area" segment
                var clean = rawPath.TrimStart('\\');
                var parts = clean.Split('\\').ToList();
                if (parts.Count >= 2 && parts[1] == "Area")
                    parts.RemoveAt(1);
                clean = string.Join("\\", parts);

                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(clean))
                    areaNodes.Add((id, clean, parentId));

                if (node.TryGetProperty("children", out var children))
                    foreach (var child in children.EnumerateArray())
                        WalkAreas(child, id);
            }
            WalkAreas(areasDoc.RootElement, null);

            // Fetch ALL CSS ACLs in one call using the root node with recurse=true
            var rootNodeId = areaNodes.Count > 0 ? areaNodes[0].Id : "";
            var allCssAcls = new Dictionary<string, Dictionary<string, AceInfo>>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(rootNodeId))
            {
                var rootToken = $"vstfs:///Classification/Node/{rootNodeId}";
                var aclUrl = $"{orgUrl}/_apis/accesscontrollists/{cssNsId}?token={Uri.EscapeDataString(rootToken)}&recurse=true&includeExtendedInfo=true&api-version=7.1";
                var aclResp = await http.GetAsync(aclUrl);
                if (aclResp.IsSuccessStatusCode)
                {
                    var aclDoc = await JsonDocument.ParseAsync(await aclResp.Content.ReadAsStreamAsync());
                    if (aclDoc.RootElement.TryGetProperty("value", out var aclArr))
                    {
                        foreach (var acl in aclArr.EnumerateArray())
                        {
                            var aclToken = acl.TryGetProperty("token", out var tk) ? tk.GetString() ?? "" : "";
                            if (!acl.TryGetProperty("acesDictionary", out var aces)) continue;
                            var tokenAcls = new Dictionary<string, AceInfo>(StringComparer.OrdinalIgnoreCase);
                            foreach (var ace in aces.EnumerateObject())
                            {
                                var allow = ace.Value.TryGetProperty("allow", out var a) ? a.GetInt32() : 0;
                                var deny = ace.Value.TryGetProperty("deny", out var d) ? d.GetInt32() : 0;
                                if (tokenAcls.TryGetValue(ace.Name, out var ex)) { ex.Allow |= allow; ex.Deny |= deny; }
                                else tokenAcls[ace.Name] = new AceInfo { Allow = allow, Deny = deny };
                            }
                            allCssAcls[aclToken] = tokenAcls;
                        }
                    }
                }
            }

            // Build node ID → parent ID map for inheritance
            var parentMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (nodeId, _, parentId) in areaNodes)
                parentMap[nodeId] = parentId;

            // Build node ID → hierarchical ACL token map.
            // CSS ACL tokens use colon-separated ancestor paths:
            //   root: vstfs:///Classification/Node/{rootId}
            //   child: vstfs:///Classification/Node/{rootId}:vstfs:///Classification/Node/{childId}
            //   grandchild: ...rootId:...childId:...grandchildId
            var nodeTokenMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (nodeId, _, _) in areaNodes)
            {
                // Build ancestor chain (root first)
                var chain = new List<string>();
                string? current = nodeId;
                while (current is not null)
                {
                    chain.Add(current);
                    current = parentMap.TryGetValue(current, out var pid) ? pid : null;
                }
                chain.Reverse();
                var hierarchicalToken = string.Join(":", chain.Select(nid => $"vstfs:///Classification/Node/{nid}"));
                nodeTokenMap[nodeId] = hierarchicalToken;
            }

            // For each area node, compute effective ACLs by walking up to root (inheritance)
            Dictionary<string, AceInfo> GetEffectiveAcls(string nodeId)
            {
                var merged = new Dictionary<string, AceInfo>(StringComparer.OrdinalIgnoreCase);
                // Collect ancestor chain (root first)
                var chain = new List<string>();
                string? current = nodeId;
                while (current is not null)
                {
                    chain.Add(current);
                    current = parentMap.TryGetValue(current, out var pid) ? pid : null;
                }
                chain.Reverse(); // root → ... → current node

                foreach (var nid in chain)
                {
                    // Use the hierarchical token for this node
                    if (!nodeTokenMap.TryGetValue(nid, out var token)) continue;
                    if (!allCssAcls.TryGetValue(token, out var nodeAces)) continue;
                    foreach (var (desc, ace) in nodeAces)
                    {
                        if (merged.TryGetValue(desc, out var ex))
                        {
                            // Child overrides parent: OR in new bits
                            ex.Allow |= ace.Allow;
                            ex.Deny |= ace.Deny;
                        }
                        else
                        {
                            merged[desc] = new AceInfo { Allow = ace.Allow, Deny = ace.Deny };
                        }
                    }
                }
                return merged;
            }

            // For each area node, compute per-member permissions using effective (inherited) ACLs
            var areas = new List<AreaPermissionEntry>();
            foreach (var (nodeId, areaPath, _) in areaNodes)
            {
                var areaAcls = GetEffectiveAcls(nodeId);

                var permEntries = new List<RepoPermAction>();
                foreach (var action in cssActions)
                {
                    var allowed = new List<string>();
                    var denied = new List<string>();
                    foreach (var member in members.Values)
                    {
                        var descriptors = new List<string>();
                        if (member.Descriptor is not null) descriptors.Add(member.Descriptor);
                        descriptors.AddRange(member.GroupDescriptors);

                        var hasAllow = false;
                        var hasDeny = false;
                        foreach (var desc in descriptors)
                        {
                            if (!areaAcls.TryGetValue(desc, out var ace)) continue;
                            if ((ace.Deny & action.Bit) != 0) hasDeny = true;
                            if ((ace.Allow & action.Bit) != 0) hasAllow = true;
                        }

                        if (hasDeny) denied.Add(member.DisplayName);
                        else if (hasAllow) allowed.Add(member.DisplayName);
                    }
                    permEntries.Add(new RepoPermAction
                    {
                        Name = action.Name,
                        DisplayName = action.DisplayName,
                        MembersAllowed = allowed.OrderBy(n => n).ToList(),
                        MembersDenied = denied.OrderBy(n => n).ToList(),
                    });
                }

                areas.Add(new AreaPermissionEntry
                {
                    AreaId = nodeId,
                    AreaPath = areaPath,
                    Permissions = permEntries,
                });
            }

            areas.Sort((a, b) => string.Compare(a.AreaPath, b.AreaPath, StringComparison.OrdinalIgnoreCase));
            var response = new AreaPermissionResponse { Areas = areas, FetchedAt = DateTime.UtcNow.ToString("o") };
            AreaCache[projectId] = (DateTime.UtcNow, response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { detail = $"Failed to fetch area permissions: {ex.Message}" });
        }
    }

    private static async Task<string?> GetNamespaceId(HttpClient http, string orgUrl, string namespaceName)
    {
        var url = $"{orgUrl}/_apis/securitynamespaces?api-version=7.1";
        var resp = await http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        if (!doc.RootElement.TryGetProperty("value", out var arr)) return null;
        foreach (var ns in arr.EnumerateArray())
        {
            var name = ns.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.Equals(name, namespaceName, StringComparison.OrdinalIgnoreCase))
                return ns.TryGetProperty("namespaceId", out var nid) ? nid.GetString() : null;
        }
        return null;
    }

    private static async Task<List<PermissionAction>> GetNamespaceActions(HttpClient http, string orgUrl, string namespaceId)
    {
        var url = $"{orgUrl}/_apis/securitynamespaces/{namespaceId}?api-version=7.1";
        var resp = await http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return [];
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var actions = new List<PermissionAction>();
        var root = doc.RootElement;
        // Response can be an object or array with single element
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            root = root[0];
        if (root.TryGetProperty("value", out var valArr) && valArr.ValueKind == JsonValueKind.Array && valArr.GetArrayLength() > 0)
            root = valArr[0];
        if (root.TryGetProperty("actions", out var actArr))
        {
            foreach (var act in actArr.EnumerateArray())
            {
                var name = act.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var display = act.TryGetProperty("displayName", out var d) ? d.GetString() ?? name : name;
                var bit = act.TryGetProperty("bit", out var b) ? b.GetInt32() : 0;
                if (bit > 0) actions.Add(new PermissionAction { Name = name, DisplayName = display, Bit = bit });
            }
        }
        return actions;
    }

    // ===================================================================
    // Pipeline Runs
    // ===================================================================

    private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, PipelineRunsResponse Data)> PipelineCache = new();

    [HttpGet("{projectId}/pipelines")]
    public async Task<IActionResult> GetPipelineRuns(string projectId,
        [FromQuery] string? minDate = null, [FromQuery] string? maxDate = null, [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var cacheKey = $"{projectId}:{minDate}:{maxDate}";
        if (!force && PipelineCache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
            return Ok(cached.Data);

        try
        {
            var http = BuildClient(pat);
            var orgUrl = $"https://dev.azure.com/{project.Organization}";

            var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/build/builds?api-version=7.1&$top=500";
            if (!string.IsNullOrEmpty(minDate)) url += $"&minTime={Uri.EscapeDataString(minDate)}";
            if (!string.IsNullOrEmpty(maxDate)) url += $"&maxTime={Uri.EscapeDataString(maxDate)}";

            var resp = await http.GetAsync(url);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return StatusCode(403, new { detail = "Missing PAT permission: Build (Read)." });
            if (!resp.IsSuccessStatusCode)
                return StatusCode(502, new { detail = "Failed to fetch builds." });

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var runs = new List<PipelineRun>();

            if (doc.RootElement.TryGetProperty("value", out var arr))
            {
                foreach (var build in arr.EnumerateArray())
                {
                    var defName = "";
                    if (build.TryGetProperty("definition", out var def))
                        defName = def.TryGetProperty("name", out var dn) ? dn.GetString() ?? "" : "";

                    var result = build.TryGetProperty("result", out var r) ? r.GetString() ?? "" : "";
                    var status = build.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                    var branch = build.TryGetProperty("sourceBranch", out var sb) ? sb.GetString() ?? "" : "";
                    // Clean branch name: "refs/heads/main" → "main"
                    if (branch.StartsWith("refs/heads/")) branch = branch["refs/heads/".Length..];

                    var requestedBy = "";
                    if (build.TryGetProperty("requestedFor", out var rf))
                        requestedBy = rf.TryGetProperty("displayName", out var rdn) ? rdn.GetString() ?? "" : "";

                    var finishedAt = build.TryGetProperty("finishTime", out var ft) ? ft.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(finishedAt))
                        finishedAt = build.TryGetProperty("startTime", out var st) ? st.GetString() ?? "" : "";

                    runs.Add(new PipelineRun
                    {
                        PipelineName = defName,
                        Result = result,
                        Status = status,
                        Branch = branch,
                        RequestedBy = requestedBy,
                        FinishedAt = finishedAt,
                    });
                }
            }

            var response = new PipelineRunsResponse { Runs = runs, FetchedAt = DateTime.UtcNow.ToString("o") };
            PipelineCache[cacheKey] = (DateTime.UtcNow, response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { detail = $"Failed to fetch pipeline runs: {ex.Message}" });
        }
    }

    // ===================================================================
    // Release Runs
    // ===================================================================

    private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, ReleaseRunsResponse Data)> ReleaseCache = new();

    [HttpGet("{projectId}/releases")]
    public async Task<IActionResult> GetReleaseRuns(string projectId,
        [FromQuery] string? minDate = null, [FromQuery] string? maxDate = null, [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var cacheKey = $"{projectId}:{minDate}:{maxDate}";
        if (!force && ReleaseCache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
            return Ok(cached.Data);

        try
        {
            var http = BuildClient(pat);
            // Release Management API uses vsrm.dev.azure.com
            var vsrmUrl = $"https://vsrm.dev.azure.com/{project.Organization}";

            var url = $"{vsrmUrl}/{Uri.EscapeDataString(project.Project)}/_apis/release/releases?api-version=7.1&$top=500&$expand=environments";
            if (!string.IsNullOrEmpty(minDate)) url += $"&minCreatedTime={Uri.EscapeDataString(minDate)}";
            if (!string.IsNullOrEmpty(maxDate)) url += $"&maxCreatedTime={Uri.EscapeDataString(maxDate)}";

            var resp = await http.GetAsync(url);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return StatusCode(403, new { detail = "Missing PAT permission: Release (Read)." });
            if (!resp.IsSuccessStatusCode)
                return StatusCode(502, new { detail = "Failed to fetch releases." });

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var runs = new List<ReleaseRun>();

            if (doc.RootElement.TryGetProperty("value", out var arr))
            {
                foreach (var rel in arr.EnumerateArray())
                {
                    var relName = rel.TryGetProperty("name", out var rn) ? rn.GetString() ?? "" : "";
                    var defName = "";
                    if (rel.TryGetProperty("releaseDefinition", out var rd))
                        defName = rd.TryGetProperty("name", out var rdn) ? rdn.GetString() ?? "" : "";

                    var status = rel.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

                    var createdBy = "";
                    if (rel.TryGetProperty("createdBy", out var cb))
                        createdBy = cb.TryGetProperty("displayName", out var cdn) ? cdn.GetString() ?? "" : "";

                    var createdOn = rel.TryGetProperty("createdOn", out var co) ? co.GetString() ?? "" : "";

                    var envs = new List<ReleaseEnvStatus>();
                    if (rel.TryGetProperty("environments", out var envArr))
                    {
                        foreach (var env in envArr.EnumerateArray())
                        {
                            var envName = env.TryGetProperty("name", out var en) ? en.GetString() ?? "" : "";
                            var envStatus = env.TryGetProperty("status", out var es) ? es.GetString() ?? "" : "";
                            envs.Add(new ReleaseEnvStatus { Name = envName, Status = envStatus });
                        }
                    }

                    runs.Add(new ReleaseRun
                    {
                        ReleaseName = relName,
                        DefinitionName = defName,
                        Status = status,
                        CreatedBy = createdBy,
                        CreatedOn = createdOn,
                        Environments = envs,
                    });
                }
            }

            var response = new ReleaseRunsResponse { Runs = runs, FetchedAt = DateTime.UtcNow.ToString("o") };
            ReleaseCache[cacheKey] = (DateTime.UtcNow, response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { detail = $"Failed to fetch releases: {ex.Message}" });
        }
    }
}
