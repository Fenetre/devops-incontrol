using DashboardApi.Models;
using DashboardApi.Services;
using DashboardApi.Services.Checks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/roadmap")]
public class RoadmapController(ConfigStore configStore) : ControllerBase
{
    // --- Configuration ---

    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(configStore.GetRoadmapConfig());

    [HttpPut("config")]
    public IActionResult SaveConfig([FromBody] RoadmapConfig config)
    {
        var saved = configStore.SaveRoadmapConfig(config);
        return Ok(saved);
    }

    // --- Lanes ---

    [HttpPost("lanes")]
    public IActionResult AddLane([FromBody] RoadmapLaneConfig lane)
    {
        var created = configStore.AddLane(lane);
        return Ok(created);
    }

    [HttpPut("lanes/{laneId}")]
    public IActionResult UpdateLane(string laneId, [FromBody] RoadmapLaneConfig lane)
    {
        var updated = configStore.UpdateLane(laneId, lane);
        if (updated is null) return NotFound(new { detail = "Lane not found" });
        return Ok(updated);
    }

    [HttpDelete("lanes/{laneId}")]
    public IActionResult DeleteLane(string laneId)
    {
        if (!configStore.DeleteLane(laneId))
            return NotFound(new { detail = "Lane not found" });
        return Ok(new { ok = true });
    }

    [HttpPost("lanes/reorder")]
    public IActionResult ReorderLanes([FromBody] List<string> laneIds)
    {
        var lanes = configStore.ReorderLanes(laneIds);
        return Ok(lanes);
    }

    // --- Projects ---

    [HttpPost("projects")]
    public IActionResult AddProject([FromBody] RoadmapProjectConfig project)
    {
        if (string.IsNullOrWhiteSpace(project.Organization) || string.IsNullOrWhiteSpace(project.ProjectId))
            return BadRequest(new { detail = "Organization and project_id are required" });

        var created = configStore.AddRoadmapProject(project);
        return Ok(created);
    }

    [HttpDelete("projects")]
    public IActionResult RemoveProject([FromQuery] string organization, [FromQuery] string projectId)
    {
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(projectId))
            return BadRequest(new { detail = "organization and projectId query params required" });

        if (!configStore.RemoveRoadmapProject(organization, projectId))
            return NotFound(new { detail = "Project not found in roadmap config" });
        return Ok(new { ok = true });
    }

    // --- Iterations (for sprint column mode) ---

    [HttpGet("iterations")]
    public async Task<IActionResult> GetIterations([FromQuery] string organization, [FromQuery] string project, [FromQuery] string team)
    {
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(team))
            return BadRequest(new { detail = "organization, project, and team query params required" });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });

        var client = new AzureDevOpsClient(organization, project, pat);
        var iterations = await client.GetIterationsAsync();

        // Fetch the team's default iteration
        string? defaultIteration = null;
        try
        {
            var http = HttpClientPool.Get(pat, 30);
            var settingsUrl = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}/_apis/work/teamsettings?api-version=7.1";
            var settingsResp = await http.GetAsync(settingsUrl);
            if (settingsResp.IsSuccessStatusCode)
            {
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(await settingsResp.Content.ReadAsStreamAsync());
                // Use backlogIteration (the team's configured root iteration),
                // not defaultIteration (which maps to the current sprint / @CurrentIteration)
                foreach (var propName in new[] { "backlogIteration", "defaultIteration" })
                {
                    if (defaultIteration is not null) break;
                    if (!doc.RootElement.TryGetProperty(propName, out var iterObj)) continue;

                    var iterName = iterObj.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";

                    // Try path first
                    var raw = iterObj.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? "" : "";

                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        // Normalize: strip leading backslash and "Iteration" segment
                        var parts = raw.Trim('\\').Split('\\');
                        if (parts.Length >= 2 && parts[1].Equals("Iteration", StringComparison.OrdinalIgnoreCase))
                            parts = [parts[0], .. parts[2..]];
                        var candidate = string.Join("\\", parts);

                        // Verify it exists in the iterations list
                        if (iterations.Any(i => string.Equals(i["path"]?.ToString(), candidate, StringComparison.OrdinalIgnoreCase)))
                        {
                            defaultIteration = candidate;
                            continue;
                        }
                    }

                    // Fallback: match by name against the iteration list
                    if (!string.IsNullOrEmpty(iterName))
                    {
                        var match = iterations.FirstOrDefault(i =>
                        {
                            var p = i["path"]?.ToString() ?? "";
                            var lastSegment = p.Contains('\\') ? p[(p.LastIndexOf('\\') + 1)..] : p;
                            return string.Equals(lastSegment, iterName, StringComparison.OrdinalIgnoreCase);
                        });
                        if (match is not null)
                        {
                            defaultIteration = match["path"]?.ToString();
                        }
                    }
                }
            }
        }
        catch { /* non-critical */ }

        return Ok(new { iterations, defaultIteration });
    }

    // --- Work Items (read from Azure DevOps) ---

    [HttpGet("items")]
    public async Task<IActionResult> GetWorkItems()
    {
        var config = configStore.GetRoadmapConfig();
        if (config.Projects.Count == 0)
            return Ok(new { items = Array.Empty<object>() });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });

        var allItems = new List<object>();

        foreach (var proj in config.Projects)
        {
            var types = proj.WorkItemTypes.Count > 0
                ? proj.WorkItemTypes
                : new List<string> { "Epic", "Feature" };

            var typeFilter = string.Join("','", types.Select(Helpers.EscapeWiql));
            var wiql = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Helpers.EscapeWiql(proj.Project)}' " +
                       $"AND [System.WorkItemType] IN ('{typeFilter}') " +
                       $"AND [System.State] NOT IN ('Closed','Removed','Done','Completed') " +
                       $"ORDER BY [Microsoft.VSTS.Common.BacklogPriority] ASC";

            try
            {
                var client = new AzureDevOpsClient(proj.Organization, proj.Project, pat);
                var ids = await client.RunWiqlAsync(wiql);
                if (ids.Count == 0) continue;

                // Use Relations expand to get parent links
                var items = await client.GetWorkItemsAsync(ids, expand: "Relations");

                foreach (var item in items)
                {
                    if (!item.TryGetProperty("fields", out var f)) continue;

                    int? parentId = null;
                    if (item.TryGetProperty("relations", out var rels) && rels.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var rel in rels.EnumerateArray())
                        {
                            var relType = rel.TryGetProperty("rel", out var rt) ? rt.GetString() : null;
                            if (relType == "System.LinkTypes.Hierarchy-Reverse")
                            {
                                // Parent link — extract ID from URL
                                var url = rel.TryGetProperty("url", out var u) ? u.GetString() : "";
                                if (url != null)
                                {
                                    var lastSlash = url.LastIndexOf('/');
                                    if (lastSlash >= 0 && int.TryParse(url[(lastSlash + 1)..], out var pid))
                                        parentId = pid;
                                }
                                break;
                            }
                        }
                    }

                    allItems.Add(new
                    {
                        id = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                        title = f.TryGetProperty("System.Title", out var t) ? t.GetString() : "",
                        work_item_type = f.TryGetProperty("System.WorkItemType", out var wt) ? wt.GetString() : "",
                        state = f.TryGetProperty("System.State", out var s) ? s.GetString() : "",
                        assigned_to = GetDisplayName(f, "System.AssignedTo"),
                        iteration_path = f.TryGetProperty("System.IterationPath", out var ip) ? ip.GetString() : "",
                        tags = f.TryGetProperty("System.Tags", out var tg) ? tg.GetString() : "",
                        start_date = f.TryGetProperty("Microsoft.VSTS.Scheduling.StartDate", out var sd) ? sd.GetString() : null,
                        target_date = f.TryGetProperty("Microsoft.VSTS.Scheduling.TargetDate", out var td) ? td.GetString() : null,
                        parent_id = parentId,
                        organization = proj.Organization,
                        project = proj.Project,
                    });
                }
            }
            catch (Exception)
            {
                allItems.Add(new
                {
                    id = 0,
                    title = "",
                    work_item_type = "",
                    state = "",
                    assigned_to = "",
                    iteration_path = "",
                    tags = "",
                    target_date = (string?)null,
                    parent_id = (int?)null,
                    organization = proj.Organization,
                    project = proj.Project,
                    error = $"Failed to load items from {proj.Organization}/{proj.Project} — see server logs.",
                });
            }
        }

        return Ok(new { items = allItems });
    }

    // --- Push positions back to Azure DevOps ---

    [HttpPost("push")]
    public async Task<IActionResult> PushPositions([FromBody] List<RoadmapPushItem> items)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });

        var results = new List<object>();

        foreach (var item in items)
        {
            try
            {
                var client = new AzureDevOpsClient(item.Organization, item.Project, pat);

                var ops = new List<object>();
                if (item.StartDate is not null)
                {
                    if (item.StartDate == "")
                        ops.Add(new { op = "remove", path = "/fields/Microsoft.VSTS.Scheduling.StartDate" });
                    else
                        ops.Add(new { op = "add", path = "/fields/Microsoft.VSTS.Scheduling.StartDate", value = item.StartDate });
                }
                if (item.TargetDate is not null)
                {
                    if (item.TargetDate == "")
                        ops.Add(new { op = "remove", path = "/fields/Microsoft.VSTS.Scheduling.TargetDate" });
                    else
                        ops.Add(new { op = "add", path = "/fields/Microsoft.VSTS.Scheduling.TargetDate", value = item.TargetDate });
                }

                if (ops.Count > 0)
                {
                    await client.PatchWorkItemFieldsAsync(item.Id, ops);
                }

                // Handle link removals (must be done before additions to keep relation indices stable)
                if (item.LinksRemoved is { Count: > 0 })
                {
                    foreach (var link in item.LinksRemoved)
                    {
                        await client.RemoveDependencyLinkAsync(item.Id, link.TargetId, link.LinkType);
                    }
                }

                // Handle link additions
                if (item.LinksAdded is { Count: > 0 })
                {
                    foreach (var link in item.LinksAdded)
                    {
                        await client.AddDependencyLinkAsync(item.Id, link.TargetId, link.LinkType);
                    }
                }

                results.Add(new { id = item.Id, ok = true });
            }
            catch (Exception)
            {
                results.Add(new { id = item.Id, ok = false, error = "Push failed — see server logs." });
            }
        }

        // Evict pushed items from cache so next load gets fresh data
        var pushedIds = results.OfType<dynamic>().Where(r => r.ok == true).Select(r => (int)r.id).ToList();
        if (pushedIds.Count > 0)
            AzureDevOpsClient.EvictWorkItems(pushedIds);

        return Ok(new { results });
    }

    private static string GetDisplayName(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var val)) return "";
        if (val.ValueKind == JsonValueKind.Object && val.TryGetProperty("displayName", out var dn))
            return dn.GetString() ?? "";
        return val.GetString() ?? "";
    }

    // --- Templates ---

    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates(
        [FromQuery] string organization, [FromQuery] string project, [FromQuery] string team)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(team))
            return BadRequest(new { detail = "organization, project, and team are required" });

        var http = HttpClientPool.Get(pat, 30);
        var url = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}/_apis/wit/templates?api-version=7.1-preview.1";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to list templates." });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var templates = new List<object>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var t in arr.EnumerateArray())
                templates.Add(new
                {
                    id = t.GetProperty("id").GetString() ?? "",
                    name = t.GetProperty("name").GetString() ?? "",
                    description = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    workItemTypeName = t.TryGetProperty("workItemTypeName", out var w) ? w.GetString() ?? "" : ""
                });

        return Ok(templates);
    }

    [HttpGet("templates/{templateId}")]
    public async Task<IActionResult> GetTemplate(string templateId,
        [FromQuery] string organization, [FromQuery] string project, [FromQuery] string team)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(team))
            return BadRequest(new { detail = "organization, project, and team are required" });

        var http = HttpClientPool.Get(pat, 30);
        var url = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}/_apis/wit/templates/{Uri.EscapeDataString(templateId)}?api-version=7.1-preview.1";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to get template." });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var root = doc.RootElement;
        var fields = new Dictionary<string, string>();
        if (root.TryGetProperty("fields", out var fObj) && fObj.ValueKind == JsonValueKind.Object)
            foreach (var prop in fObj.EnumerateObject())
                if (!string.IsNullOrEmpty(prop.Value.GetString()))
                    fields[prop.Name] = prop.Value.GetString()!;

        return Ok(new
        {
            id = root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
            fields
        });
    }

    // --- Work Item Type Fields ---

    [HttpGet("wit-fields")]
    public async Task<IActionResult> GetWorkItemTypeFields(
        [FromQuery] string organization, [FromQuery] string project, [FromQuery(Name = "type")] string workItemType)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(workItemType))
            return BadRequest(new { detail = "organization, project, and type are required" });

        var http = HttpClientPool.Get(pat, 30);
        var url = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/wit/workitemtypes/{Uri.EscapeDataString(workItemType)}/fields?$expand=All&api-version=7.1";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = $"Failed to fetch fields for {workItemType}" });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var fields = new List<object>();

        // System fields to skip (not useful for user input)
        var skipPrefixes = new[] { "System.Id", "System.Rev", "System.AuthorizedDate", "System.RevisedDate",
            "System.CreatedDate", "System.CreatedBy", "System.ChangedDate", "System.ChangedBy",
            "System.Watermark", "System.BoardColumn", "System.BoardColumnDone", "System.BoardLane",
            "System.NodeName", "System.TeamProject", "System.AreaId", "System.IterationId",
            "System.ExternalLinkCount", "System.HyperLinkCount", "System.AttachedFileCount",
            "System.RelatedLinkCount", "System.CommentCount", "System.RemoteLinkCount",
            "System.AuthorizedAs", "System.PersonId", "System.History" };
        var skipExact = new HashSet<string>(skipPrefixes) {
            "System.Title", "System.Description", "System.IterationPath", "System.AreaPath",
            "System.State", "System.Reason", "System.WorkItemType", "System.Parent"
        };

        if (doc.RootElement.TryGetProperty("value", out var arr))
        {
            foreach (var f in arr.EnumerateArray())
            {
                var refName = f.TryGetProperty("referenceName", out var rn) ? rn.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(refName)) continue;
                if (skipExact.Contains(refName)) continue;
                if (refName.StartsWith("System.") && skipPrefixes.Any(p => refName.StartsWith(p))) continue;

                var name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : refName;
                var type = f.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string";
                var readOnly = f.TryGetProperty("readOnly", out var ro) && ro.GetBoolean();
                if (readOnly) continue;

                // Get allowed values
                var allowedValues = new List<string>();
                if (f.TryGetProperty("allowedValues", out var av) && av.ValueKind == JsonValueKind.Array)
                    foreach (var v in av.EnumerateArray())
                        if (v.GetString() is { } s && s.Length > 0)
                            allowedValues.Add(s);

                var defaultValue = f.TryGetProperty("defaultValue", out var dv) ? dv.GetString() ?? "" : "";

                fields.Add(new { referenceName = refName, name, type, allowedValues, defaultValue });
            }
        }

        return Ok(fields);
    }

    // --- Create Work Item ---

    [HttpPost("create-work-item")]
    public async Task<IActionResult> CreateWorkItem([FromBody] CreateWorkItemRequest req)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });

        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { detail = "Title is required" });
        if (string.IsNullOrWhiteSpace(req.WorkItemType))
            return BadRequest(new { detail = "Work item type is required" });
        if (string.IsNullOrWhiteSpace(req.Organization) || string.IsNullOrWhiteSpace(req.Project))
            return BadRequest(new { detail = "Organization and project are required" });

        try
        {
            var ops = new List<object>
            {
                new { op = "add", path = "/fields/System.Title", value = req.Title }
            };

            if (!string.IsNullOrWhiteSpace(req.Description))
                ops.Add(new { op = "add", path = "/fields/System.Description", value = req.Description });

            if (!string.IsNullOrWhiteSpace(req.IterationPath))
                ops.Add(new { op = "add", path = "/fields/System.IterationPath", value = req.IterationPath });

            // Apply template fields
            if (req.Fields is not null)
            {
                // Fields that are set automatically or shouldn't be patched on create
                var skipFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "System.Title", "System.Description", "System.IterationPath",
                    "System.State", "System.Reason", "System.WorkItemType",
                    "System.AreaPath", "System.TeamProject", "System.Parent",
                    "System.CreatedBy", "System.CreatedDate", "System.ChangedBy", "System.ChangedDate",
                    "System.Rev", "System.Id", "System.NodeName", "System.BoardColumn",
                    "System.BoardColumnDone", "System.BoardLane", "System.History"
                };

                foreach (var (key, value) in req.Fields)
                {
                    if (skipFields.Contains(key)) continue;
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    ops.Add(new { op = "add", path = $"/fields/{key}", value });
                }
            }

            // Parent link
            if (req.ParentId is not null)
            {
                ops.Add(new
                {
                    op = "add",
                    path = "/relations/-",
                    value = new
                    {
                        rel = "System.LinkTypes.Hierarchy-Reverse",
                        url = $"https://dev.azure.com/{req.Organization}/{req.Project}/_apis/wit/workitems/{req.ParentId}",
                        attributes = new { comment = "Created from Roadmap" }
                    }
                });
            }

            var client = new AzureDevOpsClient(req.Organization, req.Project, pat);
            var result = await client.CreateWorkItemAsync(req.WorkItemType, ops);

            // Extract fields from response
            var fields = result.GetProperty("fields");
            var id = result.GetProperty("id").GetInt32();

            var response = new CreateWorkItemResponse
            {
                Id = id,
                Title = fields.TryGetProperty("System.Title", out var t) ? t.GetString() ?? "" : "",
                WorkItemType = fields.TryGetProperty("System.WorkItemType", out var wt) ? wt.GetString() ?? "" : "",
                State = fields.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "",
                AssignedTo = GetDisplayName(fields, "System.AssignedTo"),
                IterationPath = fields.TryGetProperty("System.IterationPath", out var ip) ? ip.GetString() : null,
                Tags = fields.TryGetProperty("System.Tags", out var tg) ? tg.GetString() : null,
                StartDate = fields.TryGetProperty("Microsoft.VSTS.Scheduling.StartDate", out var sd) ? sd.GetString() : null,
                TargetDate = fields.TryGetProperty("Microsoft.VSTS.Scheduling.TargetDate", out var td) ? td.GetString() : null,
                ParentId = req.ParentId,
                Organization = req.Organization,
                Project = req.Project,
            };

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to create work item — see server logs." });
        }
    }

    // --- Work Item Field Values (for edit mode) ---

    [HttpGet("work-item-field-values")]
    public async Task<IActionResult> GetWorkItemFieldValues(
        [FromQuery] string organization, [FromQuery] string project, [FromQuery(Name = "work_item_id")] int workItemId)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project) || workItemId <= 0)
            return BadRequest(new { detail = "organization, project, and work_item_id are required." });

        try
        {
            var http = HttpClientPool.Get(pat, 30);
            var url = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/wit/workitems/{workItemId}?$expand=All&api-version=7.1";
            var resp = await http.GetAsync(url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode)
                return StatusCode(502, new { detail = "Failed to fetch work item fields" });

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("fields", out var fieldsObj))
                return Ok(new Dictionary<string, string>());

            var result = new Dictionary<string, object?>();
            foreach (var prop in fieldsObj.EnumerateObject())
            {
                // Skip system fields that are already handled
                if (prop.Name is "System.Title" or "System.Description" or "System.State"
                    or "System.IterationPath" or "System.Tags" or "System.WorkItemType")
                    continue;

                // For identity fields (AssignedTo etc.), extract uniqueName
                if (prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("uniqueName", out var un))
                {
                    result[prop.Name] = un.GetString() ?? "";
                }
                else if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    result[prop.Name] = prop.Value.GetString();
                }
                else if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    result[prop.Name] = prop.Value.GetDouble();
                }
                else if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    result[prop.Name] = prop.Value.GetBoolean();
                }
                else if (prop.Value.ValueKind == JsonValueKind.Null)
                {
                    result[prop.Name] = null;
                }
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to get field values — see server logs." });
        }
    }

    // --- Update Work Item ---

    [HttpPatch("update-work-item")]
    public async Task<IActionResult> UpdateWorkItem([FromBody] UpdateWorkItemRequest req)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        if (req.WorkItemId <= 0)
            return BadRequest(new { detail = "Work item ID is required." });

        if (string.IsNullOrWhiteSpace(req.Organization) || string.IsNullOrWhiteSpace(req.Project))
            return BadRequest(new { detail = "Organization and project are required." });

        var ops = new List<object>();

        if (req.Title is not null)
            ops.Add(new { op = "add", path = "/fields/System.Title", value = req.Title });
        if (req.Description is not null)
            ops.Add(new { op = "add", path = "/fields/System.Description", value = req.Description });
        if (req.State is not null)
            ops.Add(new { op = "add", path = "/fields/System.State", value = req.State });
        if (req.AssignedTo is not null)
            ops.Add(new { op = "add", path = "/fields/System.AssignedTo", value = req.AssignedTo });
        if (req.IterationPath is not null)
            ops.Add(new { op = "add", path = "/fields/System.IterationPath", value = req.IterationPath });
        if (req.Tags is not null)
            ops.Add(new { op = "add", path = "/fields/System.Tags", value = req.Tags });

        if (req.Fields is not null)
        {
            foreach (var (key, value) in req.Fields)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                ops.Add(new { op = "add", path = $"/fields/{key}", value = value ?? "" });
            }
        }

        if (ops.Count == 0)
            return Ok(new { detail = "No changes." });

        try
        {
            var client = new AzureDevOpsClient(req.Organization, req.Project, pat);
            await client.PatchWorkItemFieldsAsync(req.WorkItemId, ops);
            return Ok(new { ok = true, id = req.WorkItemId });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { detail = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to update work item — see server logs." });
        }
    }

    // --- Work Item Type States ---

    [HttpGet("wit-states")]
    public async Task<IActionResult> GetWitStates([FromQuery] string organization, [FromQuery] string project, [FromQuery] string type)
    {
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(type))
            return BadRequest(new { detail = "organization, project, and type are required." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        try
        {
            var client = new AzureDevOpsClient(organization, project, pat);
            var url = $"{client.BaseUrl}/wit/workitemtypes/{Uri.EscapeDataString(type)}/states?api-version=7.1";
            var resp = await client.HttpClient.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var states = new List<string>();
            if (doc.RootElement.TryGetProperty("value", out var arr))
            {
                foreach (var s in arr.EnumerateArray())
                {
                    var name = s.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(name)) states.Add(name);
                }
            }
            return Ok(states);
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to get states — see server logs." });
        }
    }

    // --- Project Team Members ---

    [HttpGet("project-members")]
    public async Task<IActionResult> GetProjectMembers([FromQuery] string organization, [FromQuery] string project)
    {
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project))
            return BadRequest(new { detail = "organization and project are required." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        try
        {
            var http = HttpClientPool.Get(pat, 30);

            var orgUrl = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}";

            // Get all teams
            var teamsUrl = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(project)}/teams?api-version=7.1-preview.1&$top=300";
            var teamsResp = await http.GetAsync(teamsUrl);
            teamsResp.EnsureSuccessStatusCode();
            var teamsDoc = await JsonDocument.ParseAsync(await teamsResp.Content.ReadAsStreamAsync());

            var members = new Dictionary<string, string>(); // uniqueName -> displayName

            if (teamsDoc.RootElement.TryGetProperty("value", out var teamsArr))
            {
                foreach (var team in teamsArr.EnumerateArray())
                {
                    var teamId = team.TryGetProperty("id", out var tid) ? tid.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(teamId)) continue;

                    var membersUrl = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(project)}/teams/{teamId}/members?api-version=7.1-preview.1&$top=300";
                    var membersResp = await http.GetAsync(membersUrl);
                    if (!membersResp.IsSuccessStatusCode) continue;

                    var membersDoc = await JsonDocument.ParseAsync(await membersResp.Content.ReadAsStreamAsync());
                    if (!membersDoc.RootElement.TryGetProperty("value", out var membersArr)) continue;

                    foreach (var member in membersArr.EnumerateArray())
                    {
                        var identity = member.TryGetProperty("identity", out var nested) ? nested : member;
                        if (identity.TryGetProperty("isContainer", out var ic) && ic.ValueKind == JsonValueKind.True)
                            continue;
                        var displayName = identity.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
                        var uniqueName = identity.TryGetProperty("uniqueName", out var un) ? un.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(uniqueName))
                            members[uniqueName] = displayName;
                    }
                }
            }

            var result = members.Select(m => new { value = m.Key, label = m.Value })
                .OrderBy(m => m.label, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to get project members — see server logs." });
        }
    }

    // --- Add Comment ---

    [HttpPost("add-comment")]
    public async Task<IActionResult> AddComment([FromBody] AddCommentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Organization) || string.IsNullOrWhiteSpace(req.Project))
            return BadRequest(new { detail = "Organization and project are required." });
        if (req.WorkItemId <= 0)
            return BadRequest(new { detail = "Work item ID is required." });
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(new { detail = "Comment text is required." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        try
        {
            var http = HttpClientPool.Get(pat, 30);

            var url = $"https://dev.azure.com/{Uri.EscapeDataString(req.Organization)}/{Uri.EscapeDataString(req.Project)}/_apis/wit/workitems/{req.WorkItemId}/comments?api-version=7.1-preview.3";
            var body = new { text = req.Text };
            var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            var resp = await http.PostAsync(url, content);

            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, new { detail = $"Failed to add comment: {errBody}" });
            }

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var author = "";
            if (doc.RootElement.TryGetProperty("createdBy", out var by) && by.TryGetProperty("displayName", out var dn))
                author = dn.GetString() ?? "";
            var createdDate = doc.RootElement.TryGetProperty("createdDate", out var cd) ? cd.GetString() ?? "" : "";
            var text = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";

            return Ok(new { author, created_date = createdDate, text });
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to add comment — see server logs." });
        }
    }

    // --- Project Iterations ---

    [HttpGet("project-iterations")]
    public async Task<IActionResult> GetProjectIterations([FromQuery] string organization, [FromQuery] string project)
    {
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project))
            return BadRequest(new { detail = "organization and project are required." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        try
        {
            var http = HttpClientPool.Get(pat, 30);

            var url = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/wit/classificationnodes/Iterations?$depth=10&api-version=7.1";
            var resp = await http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());

            var paths = new List<string>();
            CollectAllIterationPaths(doc.RootElement, project, paths);
            return Ok(paths);
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to get iterations — see server logs." });
        }
    }

    private static void CollectAllIterationPaths(JsonElement node, string projectName, List<string> paths)
    {
        if (!node.TryGetProperty("children", out var children)) return;
        foreach (var child in children.EnumerateArray())
        {
            var name = child.GetProperty("name").GetString() ?? "";
            // Build full path: project\node\sub (Azure DevOps iteration path format)
            var rawPath = child.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
            // path comes as \Project\Iteration\Folder\Sprint → convert to Project\Folder\Sprint
            var parts = rawPath.Trim('\\').Split('\\').ToList();
            if (parts.Count >= 2 && parts[1].Equals("Iteration", StringComparison.OrdinalIgnoreCase))
                parts.RemoveAt(1);
            var iterPath = string.Join("\\", parts);
            if (!string.IsNullOrEmpty(iterPath))
                paths.Add(iterPath);
            CollectAllIterationPaths(child, projectName, paths);
        }
    }

    // --- Project Tags ---

    [HttpGet("project-tags")]
    public async Task<IActionResult> GetProjectTags([FromQuery] string organization, [FromQuery] string project)
    {
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project))
            return BadRequest(new { detail = "organization and project are required." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        try
        {
            var http = HttpClientPool.Get(pat, 30);

            // Use the dedicated Tags REST API
            var url = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/wit/tags?api-version=7.1";
            var resp = await http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());

            var tags = new List<string>();
            if (doc.RootElement.TryGetProperty("value", out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(name)) tags.Add(name);
                }
            }

            tags.Sort(StringComparer.OrdinalIgnoreCase);
            return Ok(tags);
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to get tags — see server logs." });
        }
    }

    // --- Upload Attachment ---

    [HttpPost("upload-attachment")]
    public async Task<IActionResult> UploadAttachment([FromForm] IFormFile file, [FromForm] string organization, [FromForm] string project)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });

        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "No file provided" });
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project))
            return BadRequest(new { detail = "Organization and project are required" });

        // Limit file size to 10MB
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { detail = "File size exceeds 10MB limit" });

        try
        {
            using var ms = new System.IO.MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var client = new AzureDevOpsClient(organization, project, pat);
            var url = await client.UploadAttachmentAsync(file.FileName, bytes, file.ContentType);

            return Ok(new UploadAttachmentResponse { Url = url });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to upload attachment — see server logs." });
        }
    }

    // --- Attachment Proxy (serves Azure DevOps attachment images through authenticated proxy) ---

    [HttpGet("attachment-proxy")]
    public async Task<IActionResult> AttachmentProxy([FromQuery] string url)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });

        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { detail = "url parameter is required" });

        // Validate that the URL is an Azure DevOps URL to prevent SSRF
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return BadRequest(new { detail = "Invalid URL" });
        var host = uri.Host.ToLowerInvariant();
        if (!host.EndsWith(".visualstudio.com") && !host.EndsWith("dev.azure.com"))
            return BadRequest(new { detail = "URL must be an Azure DevOps attachment URL" });

        try
        {
            var http = HttpClientPool.Get(pat, 30);
            var resp = await http.GetAsync(uri);
            resp.EnsureSuccessStatusCode();
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            return File(bytes, contentType);
        }
        catch (Exception)
        {
            return StatusCode(502, new { detail = "Failed to fetch attachment — see server logs." });
        }
    }

    // --- Get dependency links for a work item ---

    [HttpGet("work-item-links")]
    public async Task<IActionResult> GetWorkItemLinks(
        [FromQuery] string organization, [FromQuery] string project, [FromQuery(Name = "work_item_id")] int workItemId)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured" });

        try
        {
            var client = new AzureDevOpsClient(organization, project, pat);
            var links = await client.GetDependencyLinksAsync(workItemId);

            // Fetch titles for linked items
            var ids = links.Select(l => l.TargetId).ToList();
            var titles = new Dictionary<int, (string Title, string State, string Type)>();
            if (ids.Count > 0)
            {
                var fields = new List<string> { "System.Title", "System.State", "System.WorkItemType" };
                var items = await client.GetWorkItemsAsync(ids, fields: fields, expand: "None");
                foreach (var item in items)
                {
                    if (item.TryGetProperty("id", out var idEl) && item.TryGetProperty("fields", out var f))
                    {
                        var id = idEl.GetInt32();
                        var title = f.TryGetProperty("System.Title", out var t) ? t.GetString() ?? "" : "";
                        var state = f.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "";
                        var type = f.TryGetProperty("System.WorkItemType", out var w) ? w.GetString() ?? "" : "";
                        titles[id] = (title, state, type);
                    }
                }
            }

            var predecessors = links.Where(l => l.LinkType == "predecessor").Select(l => new
            {
                id = l.TargetId,
                title = titles.ContainsKey(l.TargetId) ? titles[l.TargetId].Title : "",
                state = titles.ContainsKey(l.TargetId) ? titles[l.TargetId].State : "",
                work_item_type = titles.ContainsKey(l.TargetId) ? titles[l.TargetId].Type : ""
            }).ToList();

            var successors = links.Where(l => l.LinkType == "successor").Select(l => new
            {
                id = l.TargetId,
                title = titles.ContainsKey(l.TargetId) ? titles[l.TargetId].Title : "",
                state = titles.ContainsKey(l.TargetId) ? titles[l.TargetId].State : "",
                work_item_type = titles.ContainsKey(l.TargetId) ? titles[l.TargetId].Type : ""
            }).ToList();

            return Ok(new { predecessors, successors });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { detail = "Failed to get links — see server logs." });
        }
    }
}

public class RoadmapPushItem
{
    public int Id { get; set; }
    public string Organization { get; set; } = "";
    public string Project { get; set; } = "";
    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }
    [JsonPropertyName("target_date")]
    public string? TargetDate { get; set; }
    [JsonPropertyName("links_added")]
    public List<RoadmapLinkChange>? LinksAdded { get; set; }
    [JsonPropertyName("links_removed")]
    public List<RoadmapLinkChange>? LinksRemoved { get; set; }
}

public class RoadmapLinkChange
{
    [JsonPropertyName("target_id")]
    public int TargetId { get; set; }
    [JsonPropertyName("link_type")]
    public string LinkType { get; set; } = ""; // "predecessor" or "successor"
}
