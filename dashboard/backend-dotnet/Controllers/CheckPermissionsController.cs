using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using DashboardApi.Models;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/check-permissions")]
public class CheckPermissionsController(ConfigStore configStore) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, PermissionAuditResponse Data)> AuditCache = new();
    private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, PeopleListResponse Data)> PeopleCache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const int MaxConcurrentProjectAudits = 6;
    private const int MaxConcurrentPeopleOrgs = 3;
    private const int MaxConcurrentPeopleProjects = 8;
    private const int MaxConcurrentTeamMembershipChecks = 6;
    private const int MaxConcurrentRepoSecurityChecks = 8;

    // Git Repositories security namespace ID (constant across all Azure DevOps orgs)
    private const string GitReposNamespaceId = "2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87";
    // Wiki security namespace ID
    private const string WikiNamespaceId = "7f36b300-2bb5-4a7f-9b70-7901d781d4e1";

    // ---------------------------------------------------------------
    // GET /api/check-permissions/people — list all users across orgs
    // ---------------------------------------------------------------
    [HttpGet("people")]
    public async Task<IActionResult> ListPeople([FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        if (!force && PeopleCache.TryGetValue("people", out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
        {
            return Ok(cached.Data);
        }

        var orgs = GetDistinctOrganizations();
        if (orgs.Count == 0)
            return Ok(new PeopleListResponse());

        var people = new ConcurrentBag<PersonInfo>();
        var seen = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var http = BuildClient(pat);
        using var orgThrottler = new SemaphoreSlim(MaxConcurrentPeopleOrgs);
        using var projectThrottler = new SemaphoreSlim(MaxConcurrentPeopleProjects);

        var orgTasks = orgs.Select(async org =>
        {
            await orgThrottler.WaitAsync();
            try
            {
                var orgUrl = $"https://dev.azure.com/{Uri.EscapeDataString(org)}";

                // List ALL projects accessible via PAT
                var projects = await ListAllProjects(http, orgUrl);

                var projectTasks = projects.Select(async p =>
                {
                    await projectThrottler.WaitAsync();
                    try
                    {
                        var projectName = p.Name;

                        // Get all teams in this project
                        var teamsUrl = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(projectName)}/teams?api-version=7.1-preview.1&$top=300";
                        var teamsResp = await http.GetAsync(teamsUrl);
                        AzureDevOpsClient.ThrowIfPatScopeError(teamsResp, teamsUrl);
                        if (!teamsResp.IsSuccessStatusCode) return;

                        var teamsDoc = await JsonDocument.ParseAsync(await teamsResp.Content.ReadAsStreamAsync());
                        if (!teamsDoc.RootElement.TryGetProperty("value", out var teamsArr)) return;

                        foreach (var team in teamsArr.EnumerateArray())
                        {
                            var teamId = team.TryGetProperty("id", out var tid) ? tid.GetString() ?? "" : "";
                            if (string.IsNullOrEmpty(teamId)) continue;

                            // Get team members
                            var membersUrl = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(projectName)}/teams/{teamId}/members?api-version=7.1-preview.1&$top=300";
                            var membersResp = await http.GetAsync(membersUrl);
                            AzureDevOpsClient.ThrowIfPatScopeError(membersResp, membersUrl);
                            if (!membersResp.IsSuccessStatusCode) continue;

                            var membersDoc = await JsonDocument.ParseAsync(await membersResp.Content.ReadAsStreamAsync());
                            if (!membersDoc.RootElement.TryGetProperty("value", out var membersArr)) continue;

                            foreach (var member in membersArr.EnumerateArray())
                            {
                                // The API may or may not wrap fields in an "identity" property
                                var identity = member.TryGetProperty("identity", out var nested) ? nested : member;

                                // Skip sub-teams (isContainer == true)
                                if (identity.TryGetProperty("isContainer", out var ic) && ic.ValueKind == JsonValueKind.True)
                                    continue;

                                var id = identity.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "";
                                var displayName = identity.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
                                var uniqueName = identity.TryGetProperty("uniqueName", out var un) ? un.GetString() ?? "" : "";

                                if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(id)) continue;

                                // Deduplicate across teams/projects by id+org
                                var key = $"{org}:{id}";
                                if (!seen.TryAdd(key, 0)) continue;

                                people.Add(new PersonInfo
                                {
                                    DisplayName = displayName,
                                    UniqueName = uniqueName,
                                    Organization = org,
                                    Descriptor = id, // Use the identity ID; we resolve groups via Identity API
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[CheckPermissions] Error listing people for org {org} project {p.Name}: {ex.Message}");
                    }
                    finally
                    {
                        projectThrottler.Release();
                    }
                });

                await Task.WhenAll(projectTasks);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CheckPermissions] Error listing people for org {org}: {ex.Message}");
            }
            finally
            {
                orgThrottler.Release();
            }
        });

        await Task.WhenAll(orgTasks);

        var response = new PeopleListResponse
        {
            People = people.OrderBy(p => p.DisplayName).ToList(),
        };

        PeopleCache["people"] = (DateTime.UtcNow, response);
        return Ok(response);
    }

    // ---------------------------------------------------------------
    // GET /api/check-permissions/person-groups?org={org}&descriptor={desc}
    // ---------------------------------------------------------------
    [HttpGet("person-groups")]
    public async Task<IActionResult> GetPersonGroups([FromQuery] string org, [FromQuery] string descriptor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(org) || string.IsNullOrWhiteSpace(descriptor))
            return BadRequest(new { detail = "Both 'org' and 'descriptor' are required." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var http = BuildClient(pat);
        var vsspsUrl = $"https://vssps.dev.azure.com/{Uri.EscapeDataString(org)}";

        var groups = new List<PersonGroupInfo>();

        try
        {
            // Use Identity API with queryMembership=Expanded (works without vso.graph scope)
            var url = $"{vsspsUrl}/_apis/identities?identityIds={Uri.EscapeDataString(descriptor)}&queryMembership=Expanded&api-version=7.1";
            var resp = await http.GetAsync(url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode)
                return StatusCode(502, new { detail = "Failed to fetch identity. Ensure your PAT includes: Identity (Read) — scope 'vso.identity'." });

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("value", out var arr) || arr.GetArrayLength() == 0)
                return Ok(new PersonSearchResponse { Results = [] });

            var identity = arr[0];

            // Extract group descriptors from memberOf
            var groupDescriptors = new List<string>();
            if (identity.TryGetProperty("memberOf", out var memberOf))
            {
                foreach (var g in memberOf.EnumerateArray())
                {
                    var desc = g.ValueKind == JsonValueKind.String
                        ? g.GetString()
                        : g.TryGetProperty("descriptor", out var gd) ? gd.GetString() : null;
                    if (!string.IsNullOrEmpty(desc))
                        groupDescriptors.Add(desc);
                }
            }

            // Resolve group descriptors to display names in batches (small batches to avoid URL length limits)
            for (var i = 0; i < groupDescriptors.Count; i += 20)
            {
                var batch = groupDescriptors.Skip(i).Take(20);
                var descParam = string.Join(",", batch);
                var resolveUrl = $"{vsspsUrl}/_apis/identities?descriptors={Uri.EscapeDataString(descParam)}&api-version=7.1";
                var resolveResp = await http.GetAsync(resolveUrl);
                if (!resolveResp.IsSuccessStatusCode) continue;

                var resolveDoc = await JsonDocument.ParseAsync(await resolveResp.Content.ReadAsStreamAsync());
                if (!resolveDoc.RootElement.TryGetProperty("value", out var resolveArr)) continue;

                foreach (var grp in resolveArr.EnumerateArray())
                {
                    var gName = grp.TryGetProperty("providerDisplayName", out var gdn) ? gdn.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(gName)) continue;
                    groups.Add(new PersonGroupInfo
                    {
                        DisplayName = gName,
                        Description = "",
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CheckPermissions] Error fetching groups for {org}: {ex.Message}");
            return StatusCode(502, new { detail = $"Failed to fetch groups: {ex.Message}" });
        }

        return Ok(new PersonSearchResponse
        {
            Results =
            [
                new PersonSearchResult
                {
                    DisplayName = "",
                    UniqueName = "",
                    Organization = org,
                    Groups = groups.OrderBy(g => g.DisplayName).ToList(),
                },
            ],
        });
    }

    // ---------------------------------------------------------------
    // POST /api/check-permissions/audit?force=false
    // ---------------------------------------------------------------
    [HttpPost("audit")]
    public async Task<IActionResult> RunAudit([FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        // Check cache
        if (!force && AuditCache.TryGetValue("audit", out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
        {
            return Ok(cached.Data);
        }

        var orgs = GetDistinctOrganizations();
        if (orgs.Count == 0)
            return Ok(new PermissionAuditResponse { FetchedAt = DateTime.UtcNow.ToString("o") });

        var denylist = configStore.GetAuditDenylist().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var auditConfig = configStore.GetAuditConfig();
        var allResults = new ConcurrentBag<ProjectAuditResult>();
        using var throttler = new SemaphoreSlim(MaxConcurrentProjectAudits);

        var orgTasks = orgs.Select(async org =>
        {
            try
            {
                var http = BuildClient(pat);
                var orgUrl = $"https://dev.azure.com/{Uri.EscapeDataString(org)}";
                var vsspsUrl = $"https://vssps.dev.azure.com/{Uri.EscapeDataString(org)}";

                // List ALL projects accessible via PAT
                var projects = await ListAllProjects(http, orgUrl);

                var projectTasks = projects
                    .Where(p => !denylist.Contains($"{org}/{p.Id}"))
                    .Select(async p =>
                    {
                        await throttler.WaitAsync();
                        try
                        {
                            var result = await AuditProject(http, orgUrl, vsspsUrl, org, p.Name, p.Id, auditConfig);
                            allResults.Add(result);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[CheckPermissions] Error auditing {org}/{p.Name}: {ex.Message}");
                            allResults.Add(new ProjectAuditResult
                            {
                                Organization = org,
                                Project = p.Name,
                                ProjectId = p.Id,
                                Checks = new ProjectAuditChecks
                                {
                                    MissingGroups = [$"Error: {ex.Message}"],
                                },
                            });
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    });

                await Task.WhenAll(projectTasks);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CheckPermissions] Error listing projects for org {org}: {ex.Message}");
            }
        });

        await Task.WhenAll(orgTasks);

        var response = new PermissionAuditResponse
        {
            Projects = allResults.OrderBy(r => r.Organization).ThenBy(r => r.Project).ToList(),
            FetchedAt = DateTime.UtcNow.ToString("o"),
        };

        AuditCache["audit"] = (DateTime.UtcNow, response);
        return Ok(response);
    }

    // ---------------------------------------------------------------
    // POST /api/check-permissions/audit/{org}/{projectId}
    // Re-audit a single project and patch the cached result.
    // ---------------------------------------------------------------
    [HttpPost("audit/{org}/{projectId}")]
    public async Task<IActionResult> RefreshSingleProject(string org, string projectId, CancellationToken cancellationToken = default)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var http = BuildClient(pat);
        var orgUrl = $"https://dev.azure.com/{Uri.EscapeDataString(org)}";
        var vsspsUrl = $"https://vssps.dev.azure.com/{Uri.EscapeDataString(org)}";

        // Resolve project name from project list
        var projects = await ListAllProjects(http, orgUrl);
        var project = projects.FirstOrDefault(p => p.Id.Equals(projectId, StringComparison.OrdinalIgnoreCase));
        if (project == default)
            return NotFound(new { detail = $"Project '{projectId}' not found in org '{org}'." });

        ProjectAuditResult result;
        var auditConfig = configStore.GetAuditConfig();
        try
        {
            result = await AuditProject(http, orgUrl, vsspsUrl, org, project.Name, project.Id, auditConfig);
        }
        catch (Exception ex)
        {
            result = new ProjectAuditResult
            {
                Organization = org,
                Project = project.Name,
                ProjectId = project.Id,
                Checks = new ProjectAuditChecks { MissingGroups = [$"Error: {ex.Message}"] },
            };
        }

        // Patch the cached audit response if it exists
        if (AuditCache.TryGetValue("audit", out var cached))
        {
            var existing = cached.Data.Projects;
            var idx = existing.FindIndex(p =>
                p.ProjectId.Equals(projectId, StringComparison.OrdinalIgnoreCase) &&
                p.Organization.Equals(org, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                existing[idx] = result;
            else
                existing.Add(result);

            AuditCache["audit"] = (cached.FetchedAt, cached.Data);
        }

        return Ok(result);
    }

    // =================================================================
    // Audit Denylist & Project Discovery
    // =================================================================

    /// <summary>List all discoverable projects across configured organizations (for scope management UI).</summary>
    [HttpGet("projects")]
    public async Task<IActionResult> ListDiscoverableProjects(CancellationToken cancellationToken = default)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var orgs = GetDistinctOrganizations();

        var orgTasks = orgs.Select(async org =>
        {
            try
            {
                var http = BuildClient(pat);
                var orgUrl = $"https://dev.azure.com/{Uri.EscapeDataString(org)}";
                var projects = await ListAllProjects(http, orgUrl);
                return projects.Select(p => (object)new { organization = org, project = p.Name, project_id = p.Id }).ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CheckPermissions] Error listing projects for org {org}: {ex.Message}");
                return new List<object>();
            }
        });

        var allResults = await Task.WhenAll(orgTasks);
        return Ok(allResults.SelectMany(r => r).ToList());
    }

    [HttpGet("denylist")]
    public List<string> GetAuditDenylist() => configStore.GetAuditDenylist();

    [HttpPost("denylist")]
    public IActionResult AddToAuditDenylist([FromBody] AuditDenylistInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Key) || !input.Key.Contains('/'))
            return BadRequest(new { detail = "Key must be in 'org/project_id' format." });
        var list = configStore.AddToAuditDenylist(input.Key);
        return Ok(list);
    }

    [HttpDelete("denylist")]
    public IActionResult RemoveFromAuditDenylist([FromBody] AuditDenylistInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Key) || !input.Key.Contains('/'))
            return BadRequest(new { detail = "Key must be in 'org/project_id' format." });
        var list = configStore.RemoveFromAuditDenylist(input.Key);
        return Ok(list);
    }

    // =================================================================
    // Audit Configuration (group connections + rules)
    // =================================================================

    [HttpGet("audit-config")]
    public AuditConfigResponse GetAuditConfig() => configStore.GetAuditConfig();

    [HttpPut("audit-config")]
    public IActionResult SaveAuditConfig([FromBody] AuditConfigInput input)
    {
        var result = configStore.SaveAuditConfig(input);
        return Ok(result);
    }

    // =================================================================
    // Private helpers
    // =================================================================

    private List<string> GetDistinctOrganizations()
    {
        return configStore.ListProjects()
            .Select(p => p.Organization)
            .Where(o => !string.IsNullOrEmpty(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HttpClient BuildClient(string pat)
        => HttpClientPool.Get(pat, 120);

    /// <summary>List all projects in the organization accessible to the PAT.</summary>
    private static async Task<List<(string Name, string Id)>> ListAllProjects(HttpClient http, string orgUrl)
    {
        var result = new List<(string, string)>();
        var continuationToken = "";

        do
        {
            var url = $"{orgUrl}/_apis/projects?$top=500&api-version=7.1";
            if (!string.IsNullOrEmpty(continuationToken))
                url += $"&continuationToken={continuationToken}";

            var resp = await http.GetAsync(url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode) break;

            // Read continuation token from response header
            continuationToken = resp.Headers.TryGetValues("x-ms-continuationtoken", out var tokens)
                ? tokens.FirstOrDefault() ?? ""
                : "";

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("value", out var arr)) break;

            foreach (var proj in arr.EnumerateArray())
            {
                var name = proj.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var id = proj.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                    result.Add((name, id));
            }
        } while (!string.IsNullOrEmpty(continuationToken));

        return result;
    }

    /// <summary>Run all audit checks for a single project.</summary>
    private static async Task<ProjectAuditResult> AuditProject(
        HttpClient http, string orgUrl, string vsspsUrl,
        string org, string projectName, string projectId,
        AuditConfigResponse auditConfig)
    {
        var checks = new ProjectAuditChecks();

        // Derive which groups matter from config (empty when not configured)
        var effectiveMandatory = auditConfig.Rules
            .Where(r => r.RuleType == "mandatory_group" && r.Enabled)
            .Select(r => r.GroupName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var repoGroups = auditConfig.GroupConfig
            .Where(gc => gc.RepoConnected)
            .Select(gc => gc.GroupName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var wikiGroups = auditConfig.GroupConfig
            .Where(gc => gc.WikiConnected)
            .Select(gc => gc.GroupName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var areaGroups = auditConfig.GroupConfig
            .Where(gc => gc.AreaConnected)
            .Select(gc => gc.GroupName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Collect all group names we need to discover
        var allGroupsToSearch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in effectiveMandatory) allGroupsToSearch.Add(g);
        foreach (var g in repoGroups) allGroupsToSearch.Add(g);
        foreach (var g in wikiGroups) allGroupsToSearch.Add(g);
        foreach (var g in areaGroups) allGroupsToSearch.Add(g);

        var teamDenyEnabled = auditConfig.Rules.Any(r => r.RuleType == "team_deny" && r.Enabled);

        // List all security groups in the project using Identity API (no Graph scope needed)
        var groups = await ListProjectGroupsViaIdentity(http, vsspsUrl, projectName, allGroupsToSearch);

        // --- Check 1: Required/mandatory groups exist ---
        var groupNameMap = new Dictionary<string, GraphGroupInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
            groupNameMap[g.DisplayName] = g;

        // Record ALL available groups (not just required ones)
        checks.AvailableGroups = groups.Select(g => g.DisplayName).OrderBy(n => n).ToList();

        foreach (var required in effectiveMandatory)
        {
            if (!groupNameMap.ContainsKey(required))
                checks.MissingGroups.Add(required);
        }

        // --- Check 2: No teams in groups (only when team_deny rule is enabled) ---
        if (teamDenyEnabled && effectiveMandatory.Count > 0)
        {
            var teamFindings = new ConcurrentBag<TeamInGroupInfo>();
            using var teamThrottler = new SemaphoreSlim(MaxConcurrentTeamMembershipChecks);

            var membershipTasks = effectiveMandatory
                .Where(required => groupNameMap.ContainsKey(required))
                .Select(async required =>
                {
                    await teamThrottler.WaitAsync();
                    try
                    {
                        var groupInfo = groupNameMap[required];
                        var members = await ListGroupMembersViaIdentity(http, vsspsUrl, groupInfo.Descriptor);
                        foreach (var member in members)
                        {
                            if (member.SubjectKind is "group" or "team")
                            {
                                teamFindings.Add(new TeamInGroupInfo
                                {
                                    Group = required,
                                    TeamName = member.DisplayName,
                                });
                            }
                        }
                    }
                    finally
                    {
                        teamThrottler.Release();
                    }
                });

            await Task.WhenAll(membershipTasks);
            checks.TeamsInGroups = teamFindings
                .OrderBy(t => t.Group, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.TeamName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // --- Check 3: Repos have repository groups in security ---
        var repos = await ListRepositories(http, orgUrl, projectName);
        if (repoGroups.Count > 0 && repos.Count > 0)
        {
            var repoFindings = new ConcurrentBag<RepoMissingGroupsInfo>();
            using var repoThrottler = new SemaphoreSlim(MaxConcurrentRepoSecurityChecks);

            var repoTasks = repos.Select(async repo =>
            {
                await repoThrottler.WaitAsync();
                try
                {
                    var missingRepoGroups = await CheckRepoSecurityGroups(
                        http, orgUrl, projectId, repo.Id, groupNameMap, repoGroups);
                    if (missingRepoGroups.Count > 0)
                    {
                        repoFindings.Add(new RepoMissingGroupsInfo
                        {
                            RepoName = repo.Name,
                            MissingGroups = missingRepoGroups,
                        });
                    }
                }
                finally
                {
                    repoThrottler.Release();
                }
            });

            await Task.WhenAll(repoTasks);
            checks.ReposMissingGroups = repoFindings
                .OrderBy(r => r.RepoName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // --- Check 4: Board groups linked to wiki security + no repo groups ---
        var wikiTask = CheckWikiSecurityGroups(
            http, orgUrl, projectName, projectId, groupNameMap, wikiGroups, repoGroups);

        // --- Check 5: Area paths have board groups in security ---
        var areaTask = CheckAreaSecurityGroups(
            http, orgUrl, projectName, groupNameMap, areaGroups);

        await Task.WhenAll(wikiTask, areaTask);
        var (wikiMissing, wikiUnwanted) = wikiTask.Result;
        checks.WikiMissingGroups = wikiMissing;
        checks.WikiUnwantedGroups = wikiUnwanted;
        checks.AreasMissingGroups = areaTask.Result;

        return new ProjectAuditResult
        {
            Organization = org,
            Project = projectName,
            ProjectId = projectId,
            Checks = checks,
        };
    }

    /// <summary>Discover ALL security groups within a project using Identity API.
    /// Uses the project's ScopeId to list all identities in the scope, then filters for groups.</summary>
    private static async Task<List<GraphGroupInfo>> ListProjectGroupsViaIdentity(HttpClient http, string vsspsUrl, string projectName, HashSet<string> groupsToSearch)
    {
        var result = new List<GraphGroupInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projectPrefix = $"[{projectName}]\\";

        // Strategy 1: Find the project scope ID via "Project Valid Users", then list all groups in that scope
        var scopeId = await FindProjectScopeId(http, vsspsUrl, projectName);
        if (!string.IsNullOrEmpty(scopeId))
        {
            // Use scopeId parameter to list ALL identities within the project scope
            var url = $"{vsspsUrl}/_apis/identities?searchFilter=General&filterValue=&queryMembership=None&scopeId={Uri.EscapeDataString(scopeId)}&api-version=7.1";
            var resp = await http.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("value", out var arr))
                {
                    foreach (var ident in arr.EnumerateArray())
                    {
                        var isContainer = ident.TryGetProperty("isContainer", out var ic) && ic.GetBoolean();
                        if (!isContainer) continue;

                        var fullName = ident.TryGetProperty("providerDisplayName", out var dn) ? dn.GetString() ?? "" : "";
                        var descriptor = ident.TryGetProperty("descriptor", out var d) ? d.GetString() ?? "" : "";

                        if (!fullName.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                        var shortName = fullName[projectPrefix.Length..];
                        if (seen.Add(shortName))
                        {
                            result.Add(new GraphGroupInfo
                            {
                                DisplayName = shortName,
                                Descriptor = descriptor,
                                Origin = "",
                            });
                        }
                    }
                }
            }
        }

        // Strategy 2: Fall back to name-based search for any groups we still haven't found
        foreach (var groupName in groupsToSearch)
        {
            if (seen.Contains(groupName)) continue;

            var url = $"{vsspsUrl}/_apis/identities?searchFilter=General&filterValue={Uri.EscapeDataString(groupName)}&queryMembership=None&api-version=7.1";
            var resp = await http.GetAsync(url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode) continue;

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("value", out var arr)) continue;

            foreach (var identity in arr.EnumerateArray())
            {
                var isContainer = identity.TryGetProperty("isContainer", out var ic) && ic.GetBoolean();
                if (!isContainer) continue;

                var fullName = identity.TryGetProperty("providerDisplayName", out var dn) ? dn.GetString() ?? "" : "";
                var descriptor = identity.TryGetProperty("descriptor", out var d) ? d.GetString() ?? "" : "";

                if (!fullName.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                var shortName = fullName[projectPrefix.Length..];
                if (!shortName.Equals(groupName, StringComparison.OrdinalIgnoreCase)) continue;

                if (seen.Add(shortName))
                {
                    result.Add(new GraphGroupInfo
                    {
                        DisplayName = shortName,
                        Descriptor = descriptor,
                        Origin = "",
                    });
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>Find the project's ScopeId by searching for "Project Valid Users" and reading its properties.</summary>
    private static async Task<string?> FindProjectScopeId(HttpClient http, string vsspsUrl, string projectName)
    {
        var searchName = "Project Valid Users";
        var projectPrefix = $"[{projectName}]\\";
        var url = $"{vsspsUrl}/_apis/identities?searchFilter=General&filterValue={Uri.EscapeDataString(searchName)}&queryMembership=None&api-version=7.1";
        var resp = await http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        if (!doc.RootElement.TryGetProperty("value", out var arr)) return null;

        foreach (var identity in arr.EnumerateArray())
        {
            var isContainer = identity.TryGetProperty("isContainer", out var ic) && ic.GetBoolean();
            if (!isContainer) continue;

            var fullName = identity.TryGetProperty("providerDisplayName", out var dn) ? dn.GetString() ?? "" : "";
            if (!fullName.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            // Read ScopeId from properties
            if (identity.TryGetProperty("properties", out var props) &&
                props.ValueKind == JsonValueKind.Object &&
                props.TryGetProperty("ScopeId", out var scopeIdProp) &&
                scopeIdProp.ValueKind == JsonValueKind.Object &&
                scopeIdProp.TryGetProperty("$value", out var sv))
            {
                return sv.GetString();
            }
        }
        return null;
    }

    /// <summary>List members of a group using Identity API (no vso.graph scope needed).</summary>
    private static async Task<List<GraphMemberInfo>> ListGroupMembersViaIdentity(HttpClient http, string vsspsUrl, string groupDescriptor)
    {
        // Query the group identity with Direct membership to get its members array
        var url = $"{vsspsUrl}/_apis/identities?descriptors={Uri.EscapeDataString(groupDescriptor)}&queryMembership=Direct&api-version=7.1";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return [];

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        if (!doc.RootElement.TryGetProperty("value", out var arr) || arr.GetArrayLength() == 0) return [];

        var groupIdentity = arr[0];

        // Extract member descriptors from the "members" array
        var memberDescriptors = new List<string>();
        if (groupIdentity.TryGetProperty("members", out var membersArr))
        {
            foreach (var m in membersArr.EnumerateArray())
            {
                var desc = m.ValueKind == JsonValueKind.String
                    ? m.GetString()
                    : m.TryGetProperty("descriptor", out var md) ? md.GetString() : null;
                if (!string.IsNullOrEmpty(desc))
                    memberDescriptors.Add(desc);
            }
        }

        if (memberDescriptors.Count == 0) return [];

        // Resolve member descriptors to get display names and isContainer
        var result = new List<GraphMemberInfo>();
        for (var i = 0; i < memberDescriptors.Count; i += 50)
        {
            var batch = memberDescriptors.Skip(i).Take(50);
            var descParam = string.Join(",", batch);
            var resolveUrl = $"{vsspsUrl}/_apis/identities?descriptors={Uri.EscapeDataString(descParam)}&api-version=7.1";
            var resolveResp = await http.GetAsync(resolveUrl);
            if (!resolveResp.IsSuccessStatusCode) continue;

            var resolveDoc = await JsonDocument.ParseAsync(await resolveResp.Content.ReadAsStreamAsync());
            if (!resolveDoc.RootElement.TryGetProperty("value", out var resolveArr)) continue;

            foreach (var ident in resolveArr.EnumerateArray())
            {
                var dName = ident.TryGetProperty("providerDisplayName", out var pdn) ? pdn.GetString() ?? "" : "";
                var isContainer = ident.TryGetProperty("isContainer", out var ic2) && ic2.GetBoolean();
                result.Add(new GraphMemberInfo
                {
                    DisplayName = dName,
                    SubjectKind = isContainer ? "group" : "user",
                });
            }
        }

        return result;
    }

    /// <summary>List all repositories in a project.</summary>
    private static async Task<List<(string Name, string Id)>> ListRepositories(
        HttpClient http, string orgUrl, string projectName)
    {
        var url = $"{orgUrl}/{Uri.EscapeDataString(projectName)}/_apis/git/repositories?api-version=7.1";
        var resp = await http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return [];

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        if (!doc.RootElement.TryGetProperty("value", out var arr)) return [];

        var result = new List<(string, string)>();
        foreach (var repo in arr.EnumerateArray())
        {
            var name = repo.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var id = repo.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                result.Add((name, id));
        }
        return result;
    }

    /// <summary>Check which required repo groups have ACEs in a specific repository's security.</summary>
    private static async Task<List<string>> CheckRepoSecurityGroups(
        HttpClient http, string orgUrl, string projectId, string repoId,
        Dictionary<string, GraphGroupInfo> groupNameMap, HashSet<string> repoGroups)
    {
        var aclDescriptors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Check both the repo-specific ACL and the project-level (inherited) ACL
        var repoToken = $"repoV2/{projectId}/{repoId}";
        var projectToken = $"repoV2/{projectId}";

        foreach (var token in new[] { repoToken, projectToken })
        {
            var aclUrl = $"{orgUrl}/_apis/accesscontrollists/{GitReposNamespaceId}?token={Uri.EscapeDataString(token)}&api-version=7.1";
            var resp = await http.GetAsync(aclUrl);
            if (!resp.IsSuccessStatusCode) continue;

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("value", out var arr)) continue;
            foreach (var acl in arr.EnumerateArray())
            {
                if (!acl.TryGetProperty("acesDictionary", out var aces)) continue;
                foreach (var ace in aces.EnumerateObject())
                    aclDescriptors.Add(ace.Name);
            }
        }

        // Check which required repo groups are present in the ACL
        var missing = new List<string>();
        foreach (var repoGroup in repoGroups)
        {
            if (!groupNameMap.TryGetValue(repoGroup, out var groupInfo))
            {
                // Group doesn't exist at all — already reported in Check 1
                continue;
            }

            // Check if this group's descriptor appears in the ACL
            if (!aclDescriptors.Contains(groupInfo.Descriptor))
                missing.Add(repoGroup);
        }

        return missing;
    }

    /// <summary>Check whether wiki-connected groups are linked to the project wiki security.</summary>
    private static async Task<(List<string> Missing, List<string> Unwanted)> CheckWikiSecurityGroups(
        HttpClient http, string orgUrl, string projectName, string projectId,
        Dictionary<string, GraphGroupInfo> groupNameMap, HashSet<string> wikiGroups, HashSet<string> repoGroups)
    {
        // Get project wikis
        var wikiUrl = $"{orgUrl}/{Uri.EscapeDataString(projectName)}/_apis/wiki/wikis?api-version=7.1";
        var wikiResp = await http.GetAsync(wikiUrl);
        if (!wikiResp.IsSuccessStatusCode)
        {
            var status = (int)wikiResp.StatusCode;
            if (status is 401 or 403)
                return (["Skipped: PAT requires 'vso.wiki' scope"], []);
            return ([$"Error: Could not list wikis ({status})"], []);
        }

        var wikiDoc = await JsonDocument.ParseAsync(await wikiResp.Content.ReadAsStreamAsync());
        if (!wikiDoc.RootElement.TryGetProperty("value", out var wikis) || wikis.GetArrayLength() == 0)
            return (["No wiki found"], []);

        // Find the project wiki (type: "projectWiki")
        string? wikiId = null;
        foreach (var wiki in wikis.EnumerateArray())
        {
            var wikiType = wiki.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            if (wikiType.Equals("projectWiki", StringComparison.OrdinalIgnoreCase))
            {
                wikiId = wiki.TryGetProperty("id", out var id) ? id.GetString() : null;
                break;
            }
        }

        if (string.IsNullOrEmpty(wikiId))
            return (["No project wiki found"], []);

        // Get ACLs for the wiki
        // Wiki security token format: {wikiId}  or  repoV2/{projectId}/{wikiId} for the backing repo
        // Try the wiki-specific namespace first
        var aclDescriptors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Check Wiki namespace
        var tokenFormats = new[] { wikiId, $"{projectId}/{wikiId}" };
        foreach (var token in tokenFormats)
        {
            var aclUrl = $"{orgUrl}/_apis/accesscontrollists/{WikiNamespaceId}?token={Uri.EscapeDataString(token)}&api-version=7.1";
            var resp = await http.GetAsync(aclUrl);
            if (!resp.IsSuccessStatusCode) continue;

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("value", out var arr)) continue;

            foreach (var acl in arr.EnumerateArray())
            {
                if (!acl.TryGetProperty("acesDictionary", out var aces)) continue;
                foreach (var ace in aces.EnumerateObject())
                    aclDescriptors.Add(ace.Name);
            }
        }

        // Also check Git Repositories namespace (wiki is backed by a git repo)
        // Check both repo-specific and project-level (inherited) tokens
        foreach (var gitToken in new[] { $"repoV2/{projectId}/{wikiId}", $"repoV2/{projectId}" })
        {
            var repoAclUrl = $"{orgUrl}/_apis/accesscontrollists/{GitReposNamespaceId}?token={Uri.EscapeDataString(gitToken)}&api-version=7.1";
            var repoResp = await http.GetAsync(repoAclUrl);
            if (!repoResp.IsSuccessStatusCode) continue;

            var repoDoc = await JsonDocument.ParseAsync(await repoResp.Content.ReadAsStreamAsync());
            if (!repoDoc.RootElement.TryGetProperty("value", out var repoArr)) continue;
            foreach (var acl in repoArr.EnumerateArray())
            {
                if (!acl.TryGetProperty("acesDictionary", out var aces)) continue;
                foreach (var ace in aces.EnumerateObject())
                    aclDescriptors.Add(ace.Name);
            }
        }

        // Check which wiki groups are present (should be) and which repo groups are present (should NOT be)
        var missing = new List<string>();
        foreach (var boardGroup in wikiGroups)
        {
            if (!groupNameMap.TryGetValue(boardGroup, out var groupInfo))
                continue; // Already flagged in Check 1

            if (!aclDescriptors.Contains(groupInfo.Descriptor))
                missing.Add(boardGroup);
        }

        var unwanted = new List<string>();
        foreach (var repoGroup in repoGroups)
        {
            if (!groupNameMap.TryGetValue(repoGroup, out var groupInfo))
                continue;

            if (aclDescriptors.Contains(groupInfo.Descriptor))
                unwanted.Add(repoGroup);
        }

        return (missing, unwanted);
    }

    /// <summary>Check whether area-connected groups are linked to area path security.</summary>
    private static async Task<List<AreaMissingGroupsInfo>> CheckAreaSecurityGroups(
        HttpClient http, string orgUrl, string projectName,
        Dictionary<string, GraphGroupInfo> groupNameMap, HashSet<string> areaGroups)
    {
        var results = new List<AreaMissingGroupsInfo>();

        // Resolve the CSS security namespace dynamically
        var cssNsId = await GetSecurityNamespaceId(http, orgUrl, "CSS");
        if (cssNsId == null) return [new AreaMissingGroupsInfo { AreaPath = "Error: Could not resolve CSS security namespace" }];

        // Get area classification tree
        var classUrl = $"{orgUrl}/{Uri.EscapeDataString(projectName)}/_apis/wit/classificationnodes/Areas?$depth=10&api-version=7.1";
        var classResp = await http.GetAsync(classUrl);
        if (!classResp.IsSuccessStatusCode) return [new AreaMissingGroupsInfo { AreaPath = "Error: Could not list areas" }];

        var classDoc = await JsonDocument.ParseAsync(await classResp.Content.ReadAsStreamAsync());
        var rootId = classDoc.RootElement.TryGetProperty("identifier", out var rid) ? rid.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(rootId)) return [new AreaMissingGroupsInfo { AreaPath = "Error: No root area node" }];

        // Get all ACLs for the CSS namespace with recurse=true from the root
        var rootToken = $"vstfs:///Classification/Node/{rootId}";
        var aclUrl = $"{orgUrl}/_apis/accesscontrollists/{cssNsId}?token={Uri.EscapeDataString(rootToken)}&recurse=true&api-version=7.1";
        var aclResp = await http.GetAsync(aclUrl);

        // Collect all ACL descriptors keyed by token
        var aclsByToken = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (aclResp.IsSuccessStatusCode)
        {
            var aclDoc = await JsonDocument.ParseAsync(await aclResp.Content.ReadAsStreamAsync());
            if (aclDoc.RootElement.TryGetProperty("value", out var aclArr))
            {
                foreach (var acl in aclArr.EnumerateArray())
                {
                    var token = acl.TryGetProperty("token", out var t) ? t.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(token)) continue;

                    if (!aclsByToken.TryGetValue(token, out var descriptors))
                    {
                        descriptors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        aclsByToken[token] = descriptors;
                    }

                    if (!acl.TryGetProperty("acesDictionary", out var aces)) continue;
                    foreach (var ace in aces.EnumerateObject())
                        descriptors.Add(ace.Name);
                }
            }
        }

        // Collect all area nodes with their paths and tokens
        var areaNodes = new List<(string Path, string Token)>();
        CollectAreaNodes(classDoc.RootElement, projectName, rootToken, areaNodes);

        // Check each area node for board groups
        foreach (var (areaPath, areaToken) in areaNodes)
        {
            // Get descriptors for this token (direct) or inherited from parent tokens
            var aclDescriptors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Check this token and all parent tokens (inheritance)
            var tokenParts = areaToken.Split(':');
            for (var i = 1; i <= tokenParts.Length; i++)
            {
                var parentToken = string.Join(":", tokenParts.Take(i));
                if (aclsByToken.TryGetValue(parentToken, out var descs))
                {
                    foreach (var d in descs)
                        aclDescriptors.Add(d);
                }
            }

            var missing = new List<string>();
            foreach (var boardGroup in areaGroups)
            {
                if (!groupNameMap.TryGetValue(boardGroup, out var groupInfo))
                    continue; // Already flagged in Check 1

                if (!aclDescriptors.Contains(groupInfo.Descriptor))
                    missing.Add(boardGroup);
            }

            if (missing.Count > 0)
            {
                results.Add(new AreaMissingGroupsInfo
                {
                    AreaPath = areaPath,
                    MissingGroups = missing,
                });
            }
        }

        return results;
    }

    /// <summary>Recursively collect area nodes with their paths and hierarchical tokens.</summary>
    private static void CollectAreaNodes(JsonElement node, string pathSoFar, string tokenSoFar, List<(string Path, string Token)> result)
    {
        var name = node.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var identifier = node.TryGetProperty("identifier", out var id) ? id.GetString() ?? "" : "";

        var currentPath = pathSoFar;
        var currentToken = tokenSoFar;

        // The root node uses the project name as path and its token is already set
        // Child nodes append to the path and token
        if (node.TryGetProperty("structureType", out _))
        {
            // This is the root node
            result.Add((currentPath, currentToken));
        }

        if (node.TryGetProperty("children", out var children))
        {
            foreach (var child in children.EnumerateArray())
            {
                var childName = child.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
                var childId = child.TryGetProperty("identifier", out var cid) ? cid.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(childId)) continue;

                var childPath = $"{currentPath}\\{childName}";
                var childToken = $"{currentToken}:vstfs:///Classification/Node/{childId}";
                result.Add((childPath, childToken));

                // Recurse into grandchildren
                CollectAreaNodes(child, childPath, childToken, result);
            }
        }
    }

    // ---------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------

    /// <summary>Resolve a security namespace ID by name (e.g., "CSS", "Git Repositories").</summary>
    private static async Task<string?> GetSecurityNamespaceId(HttpClient http, string orgUrl, string namespaceName)
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

    // ---------------------------------------------------------------
    // Internal helper types (not exposed in API)
    // ---------------------------------------------------------------
    private class GraphGroupInfo
    {
        public string DisplayName { get; set; } = "";
        public string Descriptor { get; set; } = "";
        public string Origin { get; set; } = "";
    }

    private class GraphMemberInfo
    {
        public string DisplayName { get; set; } = "";
        public string SubjectKind { get; set; } = "";
    }
}
