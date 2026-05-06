using System.Text;
using System.Text.Json;
using DashboardApi.Models;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/velocity")]
public class VelocityController(ConfigStore configStore) : ControllerBase
{
    private const int MaxConcurrentVelocityProjects = 4;

    // ---------------------------------------------------------------
    // GET /api/velocity/projects
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
    // GET /api/velocity/{projectId}/teams
    // ---------------------------------------------------------------
    [HttpGet("{projectId}/teams")]
    public async Task<IActionResult> ListTeams(string projectId, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        var url = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(project.Project)}/teams?api-version=7.1-preview.1&$top=300";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to list teams." });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var teams = new List<object>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var t in arr.EnumerateArray())
            {
                var name = t.GetProperty("name").GetString() ?? "";
                var id = t.TryGetProperty("id", out var tid) ? tid.GetString() ?? "" : "";
                teams.Add(new { id, name });
            }

        return Ok(teams);
    }

    // ---------------------------------------------------------------
    // GET /api/velocity/{projectId}/team-members?team=...
    // ---------------------------------------------------------------
    [HttpGet("{projectId}/team-members")]
    public async Task<IActionResult> ListTeamMembers(string projectId, [FromQuery] string team, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        // First resolve team ID from team name
        var teamsUrl = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(project.Project)}/teams?api-version=7.1-preview.1&$top=300";
        var teamsResp = await http.GetAsync(teamsUrl);
        AzureDevOpsClient.ThrowIfPatScopeError(teamsResp, teamsUrl);
        if (!teamsResp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to list teams." });

        var teamsDoc = await JsonDocument.ParseAsync(await teamsResp.Content.ReadAsStreamAsync());
        string? teamId = null;
        if (teamsDoc.RootElement.TryGetProperty("value", out var teamsArr))
            foreach (var t in teamsArr.EnumerateArray())
            {
                var name = t.GetProperty("name").GetString() ?? "";
                if (name.Equals(team, StringComparison.OrdinalIgnoreCase))
                {
                    teamId = t.TryGetProperty("id", out var tid) ? tid.GetString() : null;
                    break;
                }
            }

        if (teamId is null)
            return NotFound(new { detail = $"Team '{team}' not found." });

        var url = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(project.Project)}/teams/{Uri.EscapeDataString(teamId)}/members?api-version=7.1-preview.1&$top=300";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = $"Failed to list team members ({resp.StatusCode}).", url });

        var raw = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(raw);
        var members = new List<object>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var m in arr.EnumerateArray())
            {
                var id = m.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "";
                var displayName = m.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(id))
                    members.Add(new { id, display_name = displayName });
            }

        return Ok(members);
    }

    // ---------------------------------------------------------------
    // GET /api/velocity/{projectId}/iterations?team=...
    // ---------------------------------------------------------------
    [HttpGet("{projectId}/iterations")]
    public async Task<IActionResult> ListIterations(string projectId, [FromQuery] string team, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/{Uri.EscapeDataString(team)}/_apis/work/teamsettings/iterations?api-version=7.1-preview.1";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to list iterations." });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var iterations = new List<VelocityIterationInfo>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
        {
            foreach (var it in arr.EnumerateArray())
            {
                var info = new VelocityIterationInfo
                {
                    Id = it.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Name = it.GetProperty("name").GetString() ?? "",
                    Path = it.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "",
                };

                if (it.TryGetProperty("attributes", out var attrs))
                {
                    info.StartDate = attrs.TryGetProperty("startDate", out var sd) && sd.ValueKind != JsonValueKind.Null
                        ? sd.GetString() : null;
                    info.FinishDate = attrs.TryGetProperty("finishDate", out var fd) && fd.ValueKind != JsonValueKind.Null
                        ? fd.GetString() : null;
                    info.Timeframe = attrs.TryGetProperty("timeFrame", out var tf)
                        ? tf.GetString() ?? "" : "";
                }

                iterations.Add(info);
            }
        }

        return Ok(iterations);
    }

    // ---------------------------------------------------------------
    // GET /api/velocity/{projectId}/capacity?team=...&iterationId=...
    // ---------------------------------------------------------------
    [HttpGet("{projectId}/capacity")]
    public async Task<IActionResult> GetCapacity(string projectId,
        [FromQuery] string team, [FromQuery] string iterationId,
        CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        var capacityUrl = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/{Uri.EscapeDataString(team)}" +
                          $"/_apis/work/teamsettings/iterations/{Uri.EscapeDataString(iterationId)}/capacities?api-version=7.1-preview.1";
        var resp = await http.GetAsync(capacityUrl);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, capacityUrl);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to fetch capacity." });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var members = new List<CapacityMemberEntry>();
        double totalDev = 0, totalTest = 0;

        if (doc.RootElement.TryGetProperty("value", out var arr))
        {
            foreach (var cap in arr.EnumerateArray())
            {
                var entry = new CapacityMemberEntry();

                if (cap.TryGetProperty("teamMember", out var tm))
                {
                    entry.MemberId = tm.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "";
                    entry.DisplayName = tm.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
                }

                if (cap.TryGetProperty("activities", out var acts))
                {
                    foreach (var act in acts.EnumerateArray())
                    {
                        var name = act.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var cpd = act.TryGetProperty("capacityPerDay", out var c) ? c.GetDouble() : 0;
                        entry.Activities.Add(new CapacityActivity { Name = name, CapacityPerDay = cpd });
                        if (name.Equals("Development", StringComparison.OrdinalIgnoreCase)) totalDev += cpd;
                        else if (name.Equals("Testing", StringComparison.OrdinalIgnoreCase)) totalTest += cpd;
                    }
                }

                if (cap.TryGetProperty("daysOff", out var daysOff))
                {
                    foreach (var d in daysOff.EnumerateArray())
                    {
                        var start = d.TryGetProperty("start", out var s) ? s.GetString() ?? "" : "";
                        var end = d.TryGetProperty("end", out var e) ? e.GetString() ?? "" : "";
                        entry.DaysOff.Add(new CapacityDayOff { Start = start, End = end });
                    }
                }

                members.Add(entry);
            }
        }

        // Also fetch iteration name
        var iterName = await GetIterationName(http, orgUrl, project.Project, team, iterationId);

        return Ok(new TeamCapacityResponse
        {
            IterationId = iterationId,
            IterationName = iterName,
            Members = members,
            TotalDevelopment = totalDev,
            TotalTesting = totalTest,
        });
    }

    // ---------------------------------------------------------------
    // GET /api/velocity/{projectId}/previous-capacity?team=...&iterationId=...
    // Returns capacity from the most recent previous sprint that had entries.
    // ---------------------------------------------------------------
    [HttpGet("{projectId}/previous-capacity")]
    public async Task<IActionResult> GetPreviousCapacity(string projectId,
        [FromQuery] string team, [FromQuery] string iterationId,
        CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        var members = await FetchPreviousSprintCapacity(http, orgUrl, project.Project, team, iterationId);
        return Ok(new { members });
    }

    // ---------------------------------------------------------------
    // PUT /api/velocity/{projectId}/capacity — push capacity to DevOps
    // KNOWN BUG: DevOps re-populates capacity entries after a bulk PUT
    // with an empty array. The API returns 200 with count:0, but the
    // DevOps UI and subsequent GETs restore the previous members.
    // This is a DevOps server-side behaviour we cannot control.
    // ---------------------------------------------------------------
    [HttpPut("{projectId}/capacity")]
    public async Task<IActionResult> SetCapacity(string projectId, [FromBody] SetCapacityRequest body, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        var baseUrl = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/{Uri.EscapeDataString(body.Team)}" +
                      $"/_apis/work/teamsettings/iterations/{Uri.EscapeDataString(body.IterationId)}";

        // Bulk PUT to set/replace capacity for all requested members
        var bulkUrl = $"{baseUrl}/capacities?api-version=7.1-preview.1";
        var payload = body.Members.Select(m => new
        {
            teamMember = new { id = m.MemberId },
            activities = m.Activities.Select(a => new { name = a.Name, capacityPerDay = a.CapacityPerDay }),
            daysOff = m.DaysOff.Select(d => new { start = d.Start, end = d.End }),
        });

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await http.PutAsync(bulkUrl, content);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, bulkUrl);

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync();
            return StatusCode((int)resp.StatusCode, new { detail = $"DevOps returned {resp.StatusCode}: {errBody}" });
        }

        // After the bulk PUT, check if any members remain that shouldn't.
        // The bulk PUT doesn't always remove members not in the list (especially the last one).
        var requestedIds = new HashSet<string>(body.Members.Select(m => m.MemberId));
        var checkResp = await http.GetAsync(bulkUrl);
        var removed = 0;
        if (checkResp.IsSuccessStatusCode)
        {
            var doc = await JsonDocument.ParseAsync(await checkResp.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("value", out var arr))
            {
                foreach (var cap in arr.EnumerateArray())
                {
                    if (!cap.TryGetProperty("teamMember", out var tm)) continue;
                    var mid = tm.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
                    if (requestedIds.Contains(mid)) continue;

                    // This member was NOT in the request — zero them out via PATCH
                    var patchUrl = $"{baseUrl}/capacities/{Uri.EscapeDataString(mid)}?api-version=7.1-preview.1";
                    var zeroPayload = JsonSerializer.Serialize(new
                    {
                        activities = new[] { new { name = "", capacityPerDay = 0 } },
                        daysOff = Array.Empty<object>(),
                    });
                    var patchContent = new StringContent(zeroPayload, Encoding.UTF8, "application/json");
                    var patchReq = new HttpRequestMessage(HttpMethod.Patch, patchUrl) { Content = patchContent };
                    var patchResp = await http.SendAsync(patchReq);
                    if (patchResp.IsSuccessStatusCode) removed++;
                }
            }
        }

        var detail = removed > 0
            ? $"All {body.Members.Count} members updated, {removed} removed."
            : $"All {body.Members.Count} members updated successfully.";
        return Ok(new { updated = body.Members.Count, removed, detail });
    }

    // ---------------------------------------------------------------
    // GET /api/velocity/{projectId}/sprint-points?team=...&iterationId=...
    // ---------------------------------------------------------------
    [HttpGet("{projectId}/sprint-points")]
    public async Task<IActionResult> GetSprintPoints(string projectId,
        [FromQuery] string team, [FromQuery] string iterationId,
        CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        // Get iteration path from iteration ID
        var iterPath = await GetIterationPath(http, orgUrl, project.Project, team, iterationId);
        if (iterPath is null)
            return BadRequest(new { detail = "Could not resolve iteration path." });

        var iterName = iterPath.Split('\\').Last();

        // WIQL for completed items with story points
        var wiql = $"SELECT [System.Id] FROM WorkItems " +
                   $"WHERE [System.TeamProject] = '{project.Project.Replace("'", "''")}' " +
                   $"AND [System.IterationPath] UNDER '{iterPath.Replace("'", "''")}' " +
                   $"AND [System.State] IN ('Closed','Resolved','Done') " +
                   $"AND [Microsoft.VSTS.Scheduling.StoryPoints] > 0";

        var wiqlPayload = JsonSerializer.Serialize(new { query = wiql });
        var wiqlContent = new StringContent(wiqlPayload, Encoding.UTF8, "application/json");
        var wiqlUrl = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/wit/wiql?api-version=7.1";
        var wiqlResp = await http.PostAsync(wiqlUrl, wiqlContent);
        AzureDevOpsClient.ThrowIfPatScopeError(wiqlResp, wiqlUrl);
        if (!wiqlResp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to query work items." });

        var wiqlDoc = await JsonDocument.ParseAsync(await wiqlResp.Content.ReadAsStreamAsync());
        var ids = new List<int>();
        if (wiqlDoc.RootElement.TryGetProperty("workItems", out var workItems))
            foreach (var wi in workItems.EnumerateArray())
                if (wi.TryGetProperty("id", out var wid)) ids.Add(wid.GetInt32());

        if (ids.Count == 0)
            return Ok(new SprintPointsResponse { IterationId = iterationId, IterationName = iterName });

        // Batch-fetch work items
        var items = new List<SprintPointItem>();
        double totalPoints = 0;

        foreach (var batch in ids.Chunk(200))
        {
            var batchPayload = JsonSerializer.Serialize(new
            {
                ids = batch,
                fields = new[] { "System.Id", "System.Title", "System.State", "Microsoft.VSTS.Scheduling.StoryPoints" }
            });
            var batchContent = new StringContent(batchPayload, Encoding.UTF8, "application/json");
            var batchUrl = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/wit/workitemsbatch?api-version=7.1";
            var batchResp = await http.PostAsync(batchUrl, batchContent);
            if (!batchResp.IsSuccessStatusCode) continue;

            var batchDoc = await JsonDocument.ParseAsync(await batchResp.Content.ReadAsStreamAsync());
            if (!batchDoc.RootElement.TryGetProperty("value", out var batchArr)) continue;

            foreach (var wi in batchArr.EnumerateArray())
            {
                if (!wi.TryGetProperty("fields", out var fields)) continue;
                var id = wi.TryGetProperty("id", out var wid) ? wid.GetInt32() : 0;
                var title = fields.TryGetProperty("System.Title", out var t) ? t.GetString() ?? "" : "";
                var state = fields.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "";
                var sp = fields.TryGetProperty("Microsoft.VSTS.Scheduling.StoryPoints", out var pts)
                    && pts.ValueKind == JsonValueKind.Number ? pts.GetDouble() : 0;

                items.Add(new SprintPointItem { Id = id, Title = title, StoryPoints = sp, State = state });
                totalPoints += sp;
            }
        }

        return Ok(new SprintPointsResponse
        {
            IterationId = iterationId,
            IterationName = iterName,
            TotalStoryPoints = totalPoints,
            Items = items.OrderBy(i => i.Title).ToList(),
        });
    }

    // ---------------------------------------------------------------
    // GET /api/velocity/{projectId}/calculate?team=...&lastIterationId=...&targetIterationId=...
    // ---------------------------------------------------------------
    [HttpGet("{projectId}/calculate")]
    public async Task<IActionResult> CalculateVelocity(string projectId,
        [FromQuery] string team, [FromQuery] string lastIterationId,
        [FromQuery] string targetIterationId, [FromQuery] double? overridePoints = null,
        CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        // Fetch independent data in parallel
        var lastCapacityTask = FetchCapacityInternal(http, orgUrl, project.Project, team, lastIterationId);
        var lastPathTask = GetIterationPath(http, orgUrl, project.Project, team, lastIterationId);
        var targetCapacityTask = FetchCapacityInternal(http, orgUrl, project.Project, team, targetIterationId);
        await Task.WhenAll(lastCapacityTask, lastPathTask, targetCapacityTask);

        var lastCapacity = await lastCapacityTask;
        var lastPath = await lastPathTask;
        var targetCapacity = await targetCapacityTask;

        // This depends on lastPath, so it must run after
        var lastPoints = overridePoints ?? await FetchSprintPointsInternal(http, orgUrl, project.Project, team, lastIterationId, lastPath);

        var lastTotal = lastCapacity.TotalDevelopment + lastCapacity.TotalTesting;
        var targetTotal = targetCapacity.TotalDevelopment + targetCapacity.TotalTesting;
        var ratio = lastTotal > 0 ? lastPoints / lastTotal : 0;
        var projected = ratio * targetTotal;

        return Ok(new VelocityCalcResponse
        {
            LastSprint = new VelocitySprintInfo
            {
                Name = lastCapacity.IterationName,
                StoryPoints = lastPoints,
                CapacityDev = lastCapacity.TotalDevelopment,
                CapacityTest = lastCapacity.TotalTesting,
                CapacityTotal = lastTotal,
            },
            TargetSprint = new VelocitySprintInfo
            {
                Name = targetCapacity.IterationName,
                CapacityDev = targetCapacity.TotalDevelopment,
                CapacityTest = targetCapacity.TotalTesting,
                CapacityTotal = targetTotal,
            },
            VelocityRatio = Math.Round(ratio, 4),
            ProjectedPoints = Math.Round(projected, 1),
        });
    }

    // ---------------------------------------------------------------
    // GET /api/velocity/metrics?sprints=5
    // ---------------------------------------------------------------
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics([FromQuery] int sprints = 10, CancellationToken cancellationToken = default)
    {
        var pat = SettingsController.GetPat(configStore);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });

        var projects = configStore.ListProjects();
        using var projectThrottler = new SemaphoreSlim(MaxConcurrentVelocityProjects);
        var projectTasks = projects.Select(async proj =>
        {
            await projectThrottler.WaitAsync();
            try
            {
                var orgUrl = $"https://dev.azure.com/{proj.Organization}";
                var http = BuildClient(pat);

                // Get first team
                var teamsUrl = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(proj.Project)}/teams?api-version=7.1-preview.1&$top=1";
                var teamsResp = await http.GetAsync(teamsUrl);
                if (!teamsResp.IsSuccessStatusCode) return null;

                var teamsDoc = await JsonDocument.ParseAsync(await teamsResp.Content.ReadAsStreamAsync());
                if (!teamsDoc.RootElement.TryGetProperty("value", out var teamsArr) || teamsArr.GetArrayLength() == 0) return null;

                var teamName = teamsArr[0].GetProperty("name").GetString() ?? "";
                if (string.IsNullOrEmpty(teamName)) return null;

                // Get ALL iterations from project-level classification nodes (includes unsubscribed sprints)
                var iterUrl = $"{orgUrl}/{Uri.EscapeDataString(proj.Project)}/_apis/wit/classificationnodes/iterations?$depth=20&api-version=7.1";
                var iterResp = await http.GetAsync(iterUrl);
                if (!iterResp.IsSuccessStatusCode) return null;

                var iterDoc = await JsonDocument.ParseAsync(await iterResp.Content.ReadAsStreamAsync());
                var now = DateTime.UtcNow;

                // Recursively collect leaf iterations with dates
                var allIterations = new List<(string Id, string Name, string Path, string Timeframe, DateTime? FinishDate)>();
                void CollectIterations(JsonElement node, string parentPath)
                {
                    var nodeName = node.TryGetProperty("name", out var nn) ? nn.GetString() ?? "" : "";
                    var nodePath = string.IsNullOrEmpty(parentPath) ? nodeName : $"{parentPath}\\{nodeName}";

                    // Check if this node has dates (= actual sprint, not just a folder)
                    DateTime? startDate = null, finishDate = null;
                    if (node.TryGetProperty("attributes", out var attrs))
                    {
                        if (attrs.TryGetProperty("startDate", out var sd) && sd.ValueKind == JsonValueKind.String
                            && DateTime.TryParse(sd.GetString(), out var sdVal))
                            startDate = sdVal;
                        if (attrs.TryGetProperty("finishDate", out var fd) && fd.ValueKind == JsonValueKind.String
                            && DateTime.TryParse(fd.GetString(), out var fdVal))
                            finishDate = fdVal;
                    }

                    if (finishDate.HasValue)
                    {
                        // Determine timeframe from dates
                        string tf;
                        if (finishDate.Value < now && (!startDate.HasValue || startDate.Value < now))
                            tf = "past";
                        else if (startDate.HasValue && startDate.Value <= now && finishDate.Value >= now)
                            tf = "current";
                        else
                            tf = "future";

                        var id = node.TryGetProperty("identifier", out var iid) ? iid.GetString() ?? "" : "";
                        var iterPath = $"{proj.Project}\\{nodePath}";
                        allIterations.Add((id, nodeName, iterPath, tf, finishDate));
                    }

                    // Recurse into children
                    if (node.TryGetProperty("children", out var children))
                        foreach (var child in children.EnumerateArray())
                            CollectIterations(child, nodePath);
                }

                // The root node is the "Iteration" root — recurse its children
                if (iterDoc.RootElement.TryGetProperty("children", out var topChildren))
                    foreach (var child in topChildren.EnumerateArray())
                        CollectIterations(child, "");
                // If no children, check if the root itself has iterations
                else if (iterDoc.RootElement.TryGetProperty("attributes", out _))
                    CollectIterations(iterDoc.RootElement, "");

                var pastCurrentSprints = allIterations
                    .Where(i => i.Timeframe != "future")
                    .OrderByDescending(i => i.FinishDate)
                    .Take(sprints + 1) // +1 to include current alongside N past
                    .Reverse()
                    .ToList();

                var futureSprints = allIterations
                    .Where(i => i.Timeframe == "future")
                    .OrderBy(i => i.FinishDate)
                    .Take(2)
                    .ToList();

                var recentSprints = pastCurrentSprints.Concat(futureSprints).ToList();

                var sprintTasks = recentSprints.Select(async sprint =>
                {
                    var (id, name, path, timeframe, _) = sprint;
                    var scopeTask = FetchInitialScopeInternal(http, orgUrl, proj.Project, teamName, id, path);
                    var burnedTask = FetchSprintPointsInternal(http, orgUrl, proj.Project, teamName, id, path);
                    await Task.WhenAll(scopeTask, burnedTask);

                    var initialScope = scopeTask.Result;
                    var burned = burnedTask.Result;
                    return new VelocitySprintInfo
                    {
                        Name = name,
                        InitialScope = initialScope,
                        BurnedPoints = burned,
                        RemainingPoints = Math.Max(0, initialScope - burned),
                        StoryPoints = burned,
                        Timeframe = timeframe,
                    };
                });

                var sprintInfos = (await Task.WhenAll(sprintTasks)).ToList();
                return new VelocityMetricsProject
                {
                    ProjectId = proj.Id,
                    ProjectName = proj.Project,
                    TeamName = teamName,
                    Sprints = sprintInfos,
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[METRICS-DIAG] Project={proj.Project} EXCEPTION: {ex.Message}");
                // Skip projects that error
                return null;
            }
            finally
            {
                projectThrottler.Release();
            }
        }).ToList();

        var projectResults = await Task.WhenAll(projectTasks);
        var result = projectResults.Where(p => p is not null).Cast<VelocityMetricsProject>().ToList();

        return Ok(new VelocityMetricsResponse
        {
            Projects = result,
            FetchedAt = DateTime.UtcNow.ToString("o"),
        });
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    private (string? Pat, ProjectConfig? Project) Resolve(string projectId)
    {
        var pat = SettingsController.GetPat(configStore);
        var project = configStore.GetProject(projectId);
        return (pat, project);
    }

    private static HttpClient BuildClient(string pat)
        => HttpClientPool.Get(pat, 45);

    /// <summary>
    /// Find the iteration immediately before the current one (by start date),
    /// then return its capacity members with activities and capacity per day.
    /// </summary>
    private static async Task<List<CapacityMemberEntry>> FetchPreviousSprintCapacity(
        HttpClient http, string orgUrl, string project, string team, string currentIterationId)
    {
        var iterUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}" +
                      $"/_apis/work/teamsettings/iterations?api-version=7.1-preview.1";
        var iterResp = await http.GetAsync(iterUrl);
        if (!iterResp.IsSuccessStatusCode) return [];

        var iterDoc = await JsonDocument.ParseAsync(await iterResp.Content.ReadAsStreamAsync());
        if (!iterDoc.RootElement.TryGetProperty("value", out var iterArr)) return [];

        // Build list of all iterations with start dates
        var iterations = new List<(string Id, DateTime? Start)>();
        foreach (var it in iterArr.EnumerateArray())
        {
            var id = it.TryGetProperty("id", out var iid) ? iid.GetString() ?? "" : "";
            DateTime? start = null;
            if (it.TryGetProperty("attributes", out var attrs) &&
                attrs.TryGetProperty("startDate", out var sd) && sd.ValueKind != JsonValueKind.Null &&
                DateTime.TryParse(sd.GetString(), out var parsed))
                start = parsed;
            iterations.Add((id, start));
        }

        // Sort by start date ascending, find current, take the one before it
        var sorted = iterations
            .Where(i => i.Start.HasValue)
            .OrderBy(i => i.Start!.Value)
            .ToList();

        var currentIdx = sorted.FindIndex(i => i.Id == currentIterationId);
        if (currentIdx <= 0) return []; // not found or is the first iteration

        var prevIterId = sorted[currentIdx - 1].Id;

        // Fetch capacity for the previous iteration
        var capUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}" +
                     $"/_apis/work/teamsettings/iterations/{Uri.EscapeDataString(prevIterId)}/capacities?api-version=7.1-preview.1";
        var capResp = await http.GetAsync(capUrl);
        if (!capResp.IsSuccessStatusCode) return [];

        var capDoc = await JsonDocument.ParseAsync(await capResp.Content.ReadAsStreamAsync());
        if (!capDoc.RootElement.TryGetProperty("value", out var capArr) || capArr.GetArrayLength() == 0) return [];

        var members = new List<CapacityMemberEntry>();
        foreach (var cap in capArr.EnumerateArray())
        {
            var entry = new CapacityMemberEntry();
            if (cap.TryGetProperty("teamMember", out var tm))
            {
                entry.MemberId = tm.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "";
                entry.DisplayName = tm.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
            }
            if (cap.TryGetProperty("activities", out var acts))
            {
                foreach (var act in acts.EnumerateArray())
                {
                    var name = act.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var cpd = act.TryGetProperty("capacityPerDay", out var c) ? c.GetDouble() : 0;
                    entry.Activities.Add(new CapacityActivity { Name = name, CapacityPerDay = cpd });
                }
            }
            members.Add(entry);
        }
        return members;
    }

    private static async Task<string?> GetIterationPath(HttpClient http, string orgUrl,
        string project, string team, string iterationId)
    {
        var url = $"{orgUrl}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}" +
                  $"/_apis/work/teamsettings/iterations/{Uri.EscapeDataString(iterationId)}?api-version=7.1-preview.1";
        var resp = await http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        return doc.RootElement.TryGetProperty("path", out var p) ? p.GetString() : null;
    }

    private static async Task<string> GetIterationName(HttpClient http, string orgUrl,
        string project, string team, string iterationId)
    {
        var url = $"{orgUrl}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}" +
                  $"/_apis/work/teamsettings/iterations/{Uri.EscapeDataString(iterationId)}?api-version=7.1-preview.1";
        var resp = await http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return "";

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        return doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
    }

    private static async Task<TeamCapacityResponse> FetchCapacityInternal(HttpClient http,
        string orgUrl, string project, string team, string iterationId)
    {
        var result = new TeamCapacityResponse { IterationId = iterationId };

        // Fetch iteration dates and capacity in parallel
        var iterUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}" +
                      $"/_apis/work/teamsettings/iterations/{Uri.EscapeDataString(iterationId)}?api-version=7.1-preview.1";
        var capacityUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}" +
                          $"/_apis/work/teamsettings/iterations/{Uri.EscapeDataString(iterationId)}/capacities?api-version=7.1-preview.1";
        var teamDaysOffUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}" +
                             $"/_apis/work/teamsettings/iterations/{Uri.EscapeDataString(iterationId)}/teamdaysoff?api-version=7.1-preview.1";

        var iterTask = http.GetAsync(iterUrl);
        var capTask = http.GetAsync(capacityUrl);
        var teamDaysOffTask = http.GetAsync(teamDaysOffUrl);
        await Task.WhenAll(iterTask, capTask, teamDaysOffTask);

        var iterResp = await iterTask;
        var capResp = await capTask;
        var teamDaysOffResp = await teamDaysOffTask;

        if (!capResp.IsSuccessStatusCode) return result;

        // Parse iteration dates
        DateTime? iterStart = null, iterEnd = null;
        if (iterResp.IsSuccessStatusCode)
        {
            var iterDoc = await JsonDocument.ParseAsync(await iterResp.Content.ReadAsStreamAsync());
            if (iterDoc.RootElement.TryGetProperty("attributes", out var attrs))
            {
            if (attrs.TryGetProperty("startDate", out var sd) && sd.ValueKind != JsonValueKind.Null
                    && DateTime.TryParse(sd.GetString(), out var s))
                    iterStart = s;
                if (attrs.TryGetProperty("finishDate", out var fd) && fd.ValueKind != JsonValueKind.Null
                    && DateTime.TryParse(fd.GetString(), out var f))
                    iterEnd = f;
            }
            result.IterationName = iterDoc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        }
        else
        {
            result.IterationName = await GetIterationName(http, orgUrl, project, team, iterationId);
        }

        // Parse team-level days off
        var teamDaysOff = new HashSet<DateTime>();
        if (teamDaysOffResp.IsSuccessStatusCode)
        {
            var tdoDoc = await JsonDocument.ParseAsync(await teamDaysOffResp.Content.ReadAsStreamAsync());
            if (tdoDoc.RootElement.TryGetProperty("daysOff", out var daysOffArr))
            {
                foreach (var range in daysOffArr.EnumerateArray())
                {
                    var start = range.TryGetProperty("start", out var rs) ? rs.GetString() : null;
                    var end = range.TryGetProperty("end", out var re) ? re.GetString() : null;
                    if (start != null && end != null &&
                        DateTime.TryParse(start, out var ds) && DateTime.TryParse(end, out var de))
                    {
                        for (var d = ds.Date; d <= de.Date; d = d.AddDays(1))
                            teamDaysOff.Add(d);
                    }
                }
            }
        }

        // Count working days in the sprint (exclude weekends + team days off)
        int sprintWorkingDays = 0;
        var workingDates = new List<DateTime>();
        if (iterStart.HasValue && iterEnd.HasValue)
        {
            for (var d = iterStart.Value.Date; d <= iterEnd.Value.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                if (teamDaysOff.Contains(d)) continue;
                workingDates.Add(d);
                sprintWorkingDays++;
            }
        }

        // Parse member capacities and compute totals
        var capDoc = await JsonDocument.ParseAsync(await capResp.Content.ReadAsStreamAsync());
        if (!capDoc.RootElement.TryGetProperty("value", out var arr)) return result;

        double totalDev = 0, totalTest = 0;
        foreach (var cap in arr.EnumerateArray())
        {
            // Count member-specific days off
            int memberDaysOff = 0;
            if (cap.TryGetProperty("daysOff", out var daysOff))
            {
                foreach (var range in daysOff.EnumerateArray())
                {
                    var start = range.TryGetProperty("start", out var rs) ? rs.GetString() : null;
                    var end = range.TryGetProperty("end", out var re) ? re.GetString() : null;
                    if (start != null && end != null &&
                        DateTime.TryParse(start, out var ds) && DateTime.TryParse(end, out var de))
                    {
                        for (var d = ds.Date; d <= de.Date; d = d.AddDays(1))
                        {
                            if (workingDates.Contains(d)) memberDaysOff++;
                        }
                    }
                }
            }

            var effectiveDays = Math.Max(0, sprintWorkingDays - memberDaysOff);

            if (!cap.TryGetProperty("activities", out var acts)) continue;
            foreach (var act in acts.EnumerateArray())
            {
                var name = act.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var cpd = act.TryGetProperty("capacityPerDay", out var c) ? c.GetDouble() : 0;
                var total = cpd * effectiveDays;
                if (name.Equals("Development", StringComparison.OrdinalIgnoreCase)) totalDev += total;
                else if (name.Equals("Testing", StringComparison.OrdinalIgnoreCase)) totalTest += total;
            }
        }

        result.TotalDevelopment = totalDev;
        result.TotalTesting = totalTest;
        return result;
    }

    /// <summary>Fetch total story points of ALL items in the iteration (any state) = initial scope.</summary>
    private static async Task<double> FetchInitialScopeInternal(HttpClient http,
        string orgUrl, string project, string team, string iterationId, string? iterPath)
    {
        if (iterPath is null)
            iterPath = await GetIterationPath(http, orgUrl, project, team, iterationId);
        if (iterPath is null) return 0;

        var wiql = $"SELECT [System.Id] FROM WorkItems " +
                   $"WHERE [System.TeamProject] = '{project.Replace("'", "''")}' " +
                   $"AND [System.IterationPath] UNDER '{iterPath.Replace("'", "''")}' " +
                   $"AND [System.State] NOT IN ('Removed') " +
                   $"AND [Microsoft.VSTS.Scheduling.StoryPoints] > 0";

        return await FetchStoryPointsFromWiql(http, orgUrl, project, wiql);
    }

    /// <summary>Fetch story points of completed items in the iteration = burned SP.</summary>
    private static async Task<double> FetchSprintPointsInternal(HttpClient http,
        string orgUrl, string project, string team, string iterationId, string? iterPath)
    {
        if (iterPath is null)
            iterPath = await GetIterationPath(http, orgUrl, project, team, iterationId);
        if (iterPath is null) return 0;

        var wiql = $"SELECT [System.Id] FROM WorkItems " +
                   $"WHERE [System.TeamProject] = '{project.Replace("'", "''")}' " +
                   $"AND [System.IterationPath] UNDER '{iterPath.Replace("'", "''")}' " +
                   $"AND [System.State] IN ('Closed','Resolved','Done') " +
                   $"AND [Microsoft.VSTS.Scheduling.StoryPoints] > 0";

        return await FetchStoryPointsFromWiql(http, orgUrl, project, wiql);
    }

    private static async Task<double> FetchStoryPointsFromWiql(HttpClient http,
        string orgUrl, string project, string wiql)
    {

        var wiqlPayload = JsonSerializer.Serialize(new { query = wiql });
        var wiqlContent = new StringContent(wiqlPayload, Encoding.UTF8, "application/json");
        var wiqlUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/wiql?api-version=7.1";
        var wiqlResp = await http.PostAsync(wiqlUrl, wiqlContent);
        if (!wiqlResp.IsSuccessStatusCode) return 0;

        var wiqlDoc = await JsonDocument.ParseAsync(await wiqlResp.Content.ReadAsStreamAsync());
        var ids = new List<int>();
        if (wiqlDoc.RootElement.TryGetProperty("workItems", out var workItems))
            foreach (var wi in workItems.EnumerateArray())
                if (wi.TryGetProperty("id", out var wid)) ids.Add(wid.GetInt32());

        if (ids.Count == 0) return 0;

        double totalPoints = 0;
        foreach (var batch in ids.Chunk(200))
        {
            var batchPayload = JsonSerializer.Serialize(new
            {
                ids = batch,
                fields = new[] { "System.Id", "Microsoft.VSTS.Scheduling.StoryPoints" }
            });
            var batchContent = new StringContent(batchPayload, Encoding.UTF8, "application/json");
            var batchUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/workitemsbatch?api-version=7.1";
            var batchResp = await http.PostAsync(batchUrl, batchContent);
            if (!batchResp.IsSuccessStatusCode) continue;

            var batchDoc = await JsonDocument.ParseAsync(await batchResp.Content.ReadAsStreamAsync());
            if (!batchDoc.RootElement.TryGetProperty("value", out var batchArr)) continue;

            foreach (var wi in batchArr.EnumerateArray())
            {
                if (!wi.TryGetProperty("fields", out var fields)) continue;
                if (fields.TryGetProperty("Microsoft.VSTS.Scheduling.StoryPoints", out var pts)
                    && pts.ValueKind == JsonValueKind.Number)
                    totalPoints += pts.GetDouble();
            }
        }

        return totalPoints;
    }
}
