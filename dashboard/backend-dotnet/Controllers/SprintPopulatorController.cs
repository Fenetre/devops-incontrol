using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashboardApi.Models;
using DashboardApi.Services;
using Markdig;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/sprint-populator")]
public partial class SprintPopulatorController(ConfigStore configStore) : ControllerBase
{
    private const int MaxConcurrentWorkItemFetches = 10;

    [GeneratedRegex(@"\b[Ss](?:[-\s]?)(\d{1,4})\b")]
    private static partial Regex SprintSNumberRegex();

    [GeneratedRegex(@"\bSprint[-\s#]*?(\d{1,4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex SprintWordRegex();

    [GeneratedRegex(@"\d{1,4}")]
    private static partial Regex FallbackNumberRegex();

    [GeneratedRegex(@"\(?\b[Xx]-(\d{1,4})\b\)?")]
    private static partial Regex XMinusNRegex();

    [GeneratedRegex(@"\b[Xx]\b")]
    private static partial Regex StandaloneXRegex();

    // ---------------------------------------------------------------
    // GET /api/sprint-populator/projects — list configured projects
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
    // GET /api/sprint-populator/{projectId}/teams
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
                teams.Add(new { name = t.GetProperty("name").GetString() ?? "" });

        return Ok(teams);
    }

    // ---------------------------------------------------------------
    // GET /api/sprint-populator/{projectId}/iterations?team=...
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
        var iterations = new List<object>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var it in arr.EnumerateArray())
            {
                var name = it.GetProperty("name").GetString() ?? "";
                var path = it.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                iterations.Add(new { name, path });
            }

        return Ok(iterations);
    }

    // ---------------------------------------------------------------
    // POST /api/sprint-populator/{projectId}/preview
    // ---------------------------------------------------------------
    [HttpPost("{projectId}/preview")]
    public async Task<IActionResult> Preview(string projectId, [FromBody] SprintPopulatorRequest body, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Target project not found." });

        var sourceProjectId = ResolveSourceProjectId(projectId, body.SourceProjectId);
        var sourceProject = configStore.GetProject(sourceProjectId);
        if (sourceProject is null) return NotFound(new { detail = "Source project not found." });

        var sourceOrgUrl = $"https://dev.azure.com/{sourceProject.Organization}";
        var http = BuildClient(pat);

        var templates = await FetchTemplates(http, sourceOrgUrl, sourceProject.Project);
        if (templates.Count == 0)
            return Ok(new { templates = Array.Empty<object>(), sprint_number = (int?)null });

        var sprintNumber = ParseSprintNumber(body.SprintName, body.IterationPath);

        var result = new List<object>();
        foreach (var (story, tasks) in templates)
        {
            var originalTitle = GetField(story, "System.Title");
            var finalTitle = TransformTitle(originalTitle, sprintNumber);
            result.Add(new
            {
                story_id = story.GetProperty("id").GetInt32(),
                original_title = originalTitle,
                final_title = finalTitle,
                iteration_path = body.IterationPath,
                task_count = tasks.Count,
                tasks = tasks.OrderBy(t => TaskSortKey(GetField(t, "System.Title")))
                    .Select(t => new
                    {
                        task_id = t.GetProperty("id").GetInt32(),
                        title = GetField(t, "System.Title"),
                    }).ToList(),
            });
        }

        return Ok(new { templates = result, sprint_number = sprintNumber });
    }

    // ---------------------------------------------------------------
    // POST /api/sprint-populator/{projectId}/apply
    // ---------------------------------------------------------------
    [HttpPost("{projectId}/apply")]
    public async Task<IActionResult> Apply(string projectId, [FromBody] SprintPopulatorRequest body, CancellationToken cancellationToken = default)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Target project not found." });

        var sourceProjectId = ResolveSourceProjectId(projectId, body.SourceProjectId);
        var sourceProject = configStore.GetProject(sourceProjectId);
        if (sourceProject is null) return NotFound(new { detail = "Source project not found." });

        var sourceOrgUrl = $"https://dev.azure.com/{sourceProject.Organization}";
        var targetOrgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        var templates = await FetchTemplates(http, sourceOrgUrl, sourceProject.Project);
        if (templates.Count == 0)
            return Ok(new { created = Array.Empty<object>() });

        var sprintNumber = ParseSprintNumber(body.SprintName, body.IterationPath);
        var isCrossProject = !string.Equals(project.Organization, sourceProject.Organization, StringComparison.OrdinalIgnoreCase) ||
                             !string.Equals(project.Project, sourceProject.Project, StringComparison.OrdinalIgnoreCase);
        var targetAreaPathOverride = isCrossProject ? ResolveTargetAreaPath(project) : null;
        var created = new List<object>();

        foreach (var (story, tasks) in templates)
        {
            var originalTitle = GetField(story, "System.Title");
            var finalTitle = TransformTitle(originalTitle, sprintNumber);

            try
            {
                var storyOps = BuildStoryOps(story, body.IterationPath, finalTitle, targetAreaPathOverride, includeSourceParentLink: !isCrossProject);
                var newStory = await PatchCreate(http, targetOrgUrl, project.Project, "User Story", storyOps);
                var newStoryId = newStory.GetProperty("id").GetInt32();
                var newParentUrl = $"{targetOrgUrl}/_apis/wit/workItems/{newStoryId}";
                var newStoryTitle = GetField(newStory, "System.Title");

                var createdTasks = new List<object>();
                foreach (var task in tasks)
                {
                    var taskOps = BuildTaskOps(task, body.IterationPath, newParentUrl, targetAreaPathOverride);
                    var newTask = await PatchCreate(http, targetOrgUrl, project.Project, "Task", taskOps);
                    createdTasks.Add(new
                    {
                        task_id = newTask.GetProperty("id").GetInt32(),
                        title = GetField(newTask, "System.Title"),
                    });
                }

                created.Add(new
                {
                    from_story_id = story.GetProperty("id").GetInt32(),
                    new_story_id = newStoryId,
                    title = newStoryTitle,
                    tasks = createdTasks,
                });
            }
            catch (Exception ex)
            {
                created.Add(new
                {
                    from_story_id = story.GetProperty("id").GetInt32(),
                    error = ex.Message,
                });
            }
        }

        return Ok(new { created });
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
        => HttpClientPool.Get(pat, 45);

    private static string ResolveSourceProjectId(string targetProjectId, string? sourceProjectId)
        => string.IsNullOrWhiteSpace(sourceProjectId)
            ? targetProjectId
            : sourceProjectId.Trim();

    private static string ResolveTargetAreaPath(ProjectConfig targetProject)
        => string.IsNullOrWhiteSpace(targetProject.AreaPath)
            ? targetProject.Project
            : targetProject.AreaPath;

    private static async Task<List<(JsonElement Story, List<JsonElement> Tasks)>>
        FetchTemplates(HttpClient http, string orgUrl, string project)
    {
        var wiql = "SELECT [System.Id] FROM WorkItems " +
                   $"WHERE [System.TeamProject] = '{project}' " +
                   "AND [System.WorkItemType] = 'User Story' " +
                   "AND [System.Tags] CONTAINS 'sprint template' " +
                   "ORDER BY [System.ChangedDate] DESC";

        var wiqlUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/wiql?api-version=7.1";
        var body = new StringContent(JsonSerializer.Serialize(new { query = wiql }), Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(wiqlUrl, body);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, wiqlUrl);
        if (!resp.IsSuccessStatusCode) return [];

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var ids = new List<int>();
        if (doc.RootElement.TryGetProperty("workItems", out var items))
            foreach (var wi in items.EnumerateArray())
                if (wi.TryGetProperty("id", out var id))
                    ids.Add(id.GetInt32());

        if (ids.Count == 0) return [];

        var result = new List<(JsonElement Story, List<JsonElement> Tasks)>();

        // Fetch all template stories in parallel (bounded)
        var storyMap = await FetchWorkItemsMap(http, orgUrl, ids);

        // Collect child task IDs per story and dedupe all task IDs for one parallel pass
        var childTaskIdsByStory = new Dictionary<int, List<int>>();
        var allTaskIds = new HashSet<int>();
        foreach (var sid in ids)
        {
            if (!storyMap.TryGetValue(sid, out var story)) continue;
            var taskIds = GetChildTaskIds(story);
            childTaskIdsByStory[sid] = taskIds;
            foreach (var tid in taskIds)
                allTaskIds.Add(tid);
        }

        // Fetch all child tasks in parallel (bounded)
        var taskMap = allTaskIds.Count > 0
            ? await FetchWorkItemsMap(http, orgUrl, allTaskIds)
            : new Dictionary<int, JsonElement>();

        // Build output in original story order
        foreach (var sid in ids)
        {
            if (!storyMap.TryGetValue(sid, out var story)) continue;

            var tasks = new List<JsonElement>();
            if (childTaskIdsByStory.TryGetValue(sid, out var taskIds))
            {
                foreach (var tid in taskIds)
                {
                    if (taskMap.TryGetValue(tid, out var task))
                        tasks.Add(task);
                }
            }

            result.Add((story, tasks));
        }

        return result;
    }

    private static async Task<Dictionary<int, JsonElement>> FetchWorkItemsMap(
        HttpClient http, string orgUrl, IEnumerable<int> ids)
    {
        var result = new ConcurrentDictionary<int, JsonElement>();
        using var throttler = new SemaphoreSlim(MaxConcurrentWorkItemFetches);

        var tasks = ids.Distinct().Select(async id =>
        {
            await throttler.WaitAsync();
            try
            {
                var wi = await GetWorkItem(http, orgUrl, id);
                if (wi is not null)
                    result[id] = wi.Value;
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);
        return new Dictionary<int, JsonElement>(result);
    }

    private static async Task<JsonElement?> GetWorkItem(HttpClient http, string orgUrl, int id)
    {
        var url = $"{orgUrl}/_apis/wit/workitems/{id}?$expand=Relations&api-version=7.1";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return null;
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        return doc.RootElement.Clone();
    }

    private static List<int> GetChildTaskIds(JsonElement item)
    {
        var ids = new List<int>();
        if (!item.TryGetProperty("relations", out var rels)) return ids;
        foreach (var r in rels.EnumerateArray())
        {
            if (r.TryGetProperty("rel", out var rel) &&
                rel.GetString() == "System.LinkTypes.Hierarchy-Forward" &&
                r.TryGetProperty("url", out var urlProp))
            {
                var url = urlProp.GetString() ?? "";
                var lastSlash = url.TrimEnd('/').LastIndexOf('/');
                if (lastSlash >= 0 && int.TryParse(url[(lastSlash + 1)..].TrimEnd('/'), out var tid))
                    ids.Add(tid);
            }
        }
        return ids;
    }

    private static string? GetParentUrl(JsonElement item)
    {
        if (!item.TryGetProperty("relations", out var rels)) return null;
        foreach (var r in rels.EnumerateArray())
        {
            if (r.TryGetProperty("rel", out var rel) &&
                rel.GetString() == "System.LinkTypes.Hierarchy-Reverse" &&
                r.TryGetProperty("url", out var urlProp))
                return urlProp.GetString();
        }
        return null;
    }

    private static string GetField(JsonElement item, string fieldName)
    {
        if (item.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty(fieldName, out var val))
        {
            if (val.ValueKind == JsonValueKind.String) return val.GetString() ?? "";
            if (val.ValueKind == JsonValueKind.Object &&
                val.TryGetProperty("displayName", out var dn))
                return dn.GetString() ?? "";
            if (val.ValueKind == JsonValueKind.Number) return val.ToString();
        }
        return "";
    }

    private static string? GetAssignee(JsonElement item)
    {
        if (item.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty("System.AssignedTo", out var val) &&
            val.ValueKind == JsonValueKind.Object)
        {
            return val.TryGetProperty("uniqueName", out var un)
                ? un.GetString()
                : val.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
        }
        return null;
    }

    private static JsonElement? GetFieldRaw(JsonElement item, string fieldName)
    {
        if (item.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty(fieldName, out var val))
            return val;
        return null;
    }

    private static int TaskSortKey(string title)
    {
        var m = FallbackNumberRegex().Match(title);
        return m.Success && int.TryParse(m.Value, out var n) ? n : int.MaxValue;
    }

    private static int? ParseSprintNumber(string sprintName, string iterationPath)
    {
        // 1) S<number> patterns
        foreach (var text in new[] { sprintName, iterationPath })
        {
            if (string.IsNullOrEmpty(text)) continue;
            var m = SprintSNumberRegex().Match(text);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) return n;
        }
        // 2) Sprint <number>
        foreach (var text in new[] { sprintName, iterationPath })
        {
            if (string.IsNullOrEmpty(text)) continue;
            var m = SprintWordRegex().Match(text);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) return n;
        }
        // 3) Last number
        foreach (var text in new[] { sprintName, iterationPath })
        {
            if (string.IsNullOrEmpty(text)) continue;
            int? last = null;
            foreach (Match m in FallbackNumberRegex().Matches(text))
                if (int.TryParse(m.Value, out var n)) last = n;
            if (last.HasValue) return last;
        }
        return null;
    }

    private static string TransformTitle(string title, int? sprintNumber)
    {
        if (sprintNumber is null) return title;
        var sn = sprintNumber.Value;
        var result = XMinusNRegex().Replace(title, m =>
        {
            var offset = int.Parse(m.Groups[1].Value);
            return Math.Max(sn - offset, 0).ToString();
        });
        result = StandaloneXRegex().Replace(result, sn.ToString());
        return result;
    }

    private static string StripTemplateTag(string? tags)
    {
        if (string.IsNullOrEmpty(tags)) return "";
        var parts = tags.Split(';').Select(t => t.Trim()).Where(t => t.Length > 0);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cleaned = new List<string>();
        foreach (var t in parts)
        {
            if (t.Equals("sprint template", StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Add(t)) continue;
            cleaned.Add(t);
        }
        return string.Join("; ", cleaned);
    }

    private static List<object> BuildStoryOps(
        JsonElement story,
        string iterationPath,
        string finalTitle,
        string? targetAreaPath = null,
        bool includeSourceParentLink = true)
    {
        var sf = story.GetProperty("fields");
        var cleanedTags = StripTemplateTag(sf.TryGetProperty("System.Tags", out var tags) ? tags.GetString() : null);
        var assignee = GetAssignee(story);
        var parentUrl = GetParentUrl(story);
        var areaPath = !string.IsNullOrWhiteSpace(targetAreaPath)
            ? targetAreaPath
            : sf.TryGetProperty("System.AreaPath", out var ap) ? ap.GetString() ?? "" : "";

        var ops = new List<object>
        {
            new { op = "add", path = "/fields/System.Title", value = (object)finalTitle },
            new { op = "add", path = "/fields/System.Description", value = (object)SanitizeHtml(sf.TryGetProperty("System.Description", out var desc) ? desc.GetString() : null) },
            new { op = "add", path = "/fields/System.AreaPath", value = (object)areaPath },
            new { op = "add", path = "/fields/System.IterationPath", value = (object)iterationPath },
        };

        if (!string.IsNullOrEmpty(cleanedTags))
            ops.Insert(2, new { op = "add", path = "/fields/System.Tags", value = (object)cleanedTags });

        if (assignee is not null)
            ops.Add(new { op = "add", path = "/fields/System.AssignedTo", value = (object)assignee });

        if (includeSourceParentLink && parentUrl is not null)
            ops.Add(new { op = "add", path = "/relations/-", value = (object)new { rel = "System.LinkTypes.Hierarchy-Reverse", url = parentUrl } });

        AddFieldIfPresent(ops, sf, "Microsoft.VSTS.Scheduling.StoryPoints");
        AddFieldIfPresent(ops, sf, "Microsoft.VSTS.Scheduling.Effort");
        AddHtmlFieldIfPresent(ops, sf, "Microsoft.VSTS.Common.AcceptanceCriteria");
        AddFieldIfPresent(ops, sf, "Microsoft.VSTS.Common.Priority");
        AddFieldIfPresent(ops, sf, "Custom.Releaseinfo");

        return ops;
    }

    private static List<object> BuildTaskOps(
        JsonElement task,
        string iterationPath,
        string newParentUrl,
        string? targetAreaPath = null)
    {
        var tf = task.GetProperty("fields");
        var cleanedTags = StripTemplateTag(tf.TryGetProperty("System.Tags", out var tags) ? tags.GetString() : null);
        var assignee = GetAssignee(task);
        var areaPath = !string.IsNullOrWhiteSpace(targetAreaPath)
            ? targetAreaPath
            : tf.TryGetProperty("System.AreaPath", out var ap) ? ap.GetString() ?? "" : "";

        var ops = new List<object>
        {
            new { op = "add", path = "/fields/System.Title", value = (object)(tf.TryGetProperty("System.Title", out var t) ? t.GetString() ?? "" : "") },
            new { op = "add", path = "/fields/System.Description", value = (object)SanitizeHtml(tf.TryGetProperty("System.Description", out var d) ? d.GetString() : null) },
            new { op = "add", path = "/fields/System.AreaPath", value = (object)areaPath },
            new { op = "add", path = "/fields/System.IterationPath", value = (object)iterationPath },
            new { op = "add", path = "/relations/-", value = (object)new { rel = "System.LinkTypes.Hierarchy-Reverse", url = newParentUrl } },
        };

        if (!string.IsNullOrEmpty(cleanedTags))
            ops.Insert(2, new { op = "add", path = "/fields/System.Tags", value = (object)cleanedTags });

        if (assignee is not null)
            ops.Add(new { op = "add", path = "/fields/System.AssignedTo", value = (object)assignee });

        AddFieldIfPresent(ops, tf, "Microsoft.VSTS.Common.Priority");
        AddFieldIfPresent(ops, tf, "Custom.Type");

        return ops;
    }

    private static void AddFieldIfPresent(List<object> ops, JsonElement fields, string fieldName)
    {
        if (fields.TryGetProperty(fieldName, out var val) && val.ValueKind != JsonValueKind.Null)
        {
            object value = val.ValueKind switch
            {
                JsonValueKind.String => val.GetString() ?? "",
                JsonValueKind.Number => val.GetDouble(),
                _ => val.GetRawText(),
            };
            ops.Add(new { op = "add", path = $"/fields/{fieldName}", value });
        }
    }

    private static readonly MarkdownPipeline MdPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseTaskLists()
        .Build();

    /// <summary>
    /// Ensure content is HTML so DevOps renders it correctly.
    /// Markdown content is converted to HTML via Markdig; content that is
    /// already HTML is passed through unchanged.
    /// </summary>
    private static string SanitizeHtml(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";

        // If the content has block-level HTML tags, it's already HTML — pass through.
        if (Regex.IsMatch(content, @"<(?:div|p|h[1-6]|ul|ol|table|tr|td|th)\b", RegexOptions.IgnoreCase))
            return content;

        // Otherwise treat as Markdown and convert to HTML.
        return Markdown.ToHtml(content, MdPipeline);
    }

    private static void AddHtmlFieldIfPresent(List<object> ops, JsonElement fields, string fieldName)
    {
        if (fields.TryGetProperty(fieldName, out var val) && val.ValueKind == JsonValueKind.String)
        {
            var sanitized = SanitizeHtml(val.GetString());
            if (!string.IsNullOrEmpty(sanitized))
                ops.Add(new { op = "add", path = $"/fields/{fieldName}", value = (object)sanitized });
        }
    }

    private static async Task<JsonElement> PatchCreate(HttpClient http, string orgUrl, string project, string workItemType, List<object> ops)
    {
        var encodedType = Uri.EscapeDataString($"${workItemType}");
        var url = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/workitems/{encodedType}?api-version=7.1";
        var content = new StringContent(JsonSerializer.Serialize(ops), Encoding.UTF8, "application/json-patch+json");
        var resp = await http.PatchAsync(url, content);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create {workItemType}: {resp.StatusCode} — {body}");
        }
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        return doc.RootElement.Clone();
    }
}

public class SprintPopulatorRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("iteration_path")]
    public string IterationPath { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("sprint_name")]
    public string SprintName { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("source_project_id")]
    public string? SourceProjectId { get; set; }
}
