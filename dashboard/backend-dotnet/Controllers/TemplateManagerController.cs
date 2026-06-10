using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DashboardApi.Models;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/template-manager")]
public class TemplateManagerController(ConfigStore configStore) : ControllerBase
{
    private const int MaxConcurrency = 10;

    [HttpGet("projects")]
    public List<object> ListProjects()
    {
        return configStore.ListProjects()
            .Select(p => new { p.Id, p.Organization, p.Project })
            .Cast<object>()
            .ToList();
    }

    [HttpGet("{projectId}/teams")]
    public async Task<IActionResult> ListTeams(string projectId)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var http = BuildClient(pat);

        var url = $"{orgUrl}/_apis/projects/{Uri.EscapeDataString(project.Project)}/teams?api-version=7.1-preview.1&$top=300";
        var resp = await GetWithRetry(http, url);
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

    [HttpGet("{projectId}/templates")]
    public async Task<IActionResult> ListTemplates(string projectId, [FromQuery] string team)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });
        if (string.IsNullOrWhiteSpace(team)) return BadRequest(new { detail = "Team is required." });

        var http = BuildClient(pat);
        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/{Uri.EscapeDataString(team)}/_apis/wit/templates?api-version=7.1-preview.1";
        var resp = await GetWithRetry(http, url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to list templates." });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var templates = new List<object>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var t in arr.EnumerateArray())
            {
                templates.Add(new
                {
                    id = t.GetProperty("id").GetString() ?? "",
                    name = t.GetProperty("name").GetString() ?? "",
                    description = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    workItemTypeName = t.TryGetProperty("workItemTypeName", out var w) ? w.GetString() ?? "" : ""
                });
            }

        return Ok(templates);
    }

    [HttpGet("{projectId}/templates/{templateId}")]
    public async Task<IActionResult> GetTemplate(string projectId, string templateId, [FromQuery] string team)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });
        if (string.IsNullOrWhiteSpace(team)) return BadRequest(new { detail = "Team is required." });

        var http = BuildClient(pat);
        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/{Uri.EscapeDataString(team)}/_apis/wit/templates/{Uri.EscapeDataString(templateId)}?api-version=7.1-preview.1";
        var resp = await GetWithRetry(http, url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to fetch template." });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var root = doc.RootElement;
        var fields = new Dictionary<string, string>();
        if (root.TryGetProperty("fields", out var fObj))
            foreach (var kv in fObj.EnumerateObject())
                fields[kv.Name] = kv.Value.GetString() ?? "";

        return Ok(new
        {
            id = root.GetProperty("id").GetString() ?? "",
            name = root.GetProperty("name").GetString() ?? "",
            description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            workItemTypeName = root.TryGetProperty("workItemTypeName", out var w) ? w.GetString() ?? "" : "",
            fields
        });
    }

    [HttpGet("{projectId}/work-item-types")]
    public async Task<IActionResult> ListWorkItemTypes(string projectId)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });

        var http = BuildClient(pat);
        var orgUrl = $"https://dev.azure.com/{project.Organization}";

        // Fetch work item type categories to identify hidden types
        var catUrl = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/wit/workitemtypecategories?api-version=7.1";
        var catResp = await GetWithRetry(http, catUrl);
        var hiddenTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (catResp.IsSuccessStatusCode)
        {
            var catDoc = await JsonDocument.ParseAsync(await catResp.Content.ReadAsStreamAsync());
            if (catDoc.RootElement.TryGetProperty("value", out var cats))
                foreach (var cat in cats.EnumerateArray())
                {
                    var refName = cat.TryGetProperty("referenceName", out var rn) ? rn.GetString() ?? "" : "";
                    if (!refName.Equals("Microsoft.HiddenCategory", StringComparison.OrdinalIgnoreCase)) continue;
                    if (cat.TryGetProperty("workItemTypes", out var witArr))
                        foreach (var wit in witArr.EnumerateArray())
                            hiddenTypes.Add(wit.TryGetProperty("name", out var wn) ? wn.GetString() ?? "" : "");
                }
        }

        var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/wit/workitemtypes?api-version=7.1";
        var resp = await GetWithRetry(http, url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to list work item types." });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var types = new List<object>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var t in arr.EnumerateArray())
            {
                var name = t.GetProperty("name").GetString() ?? "";
                if (hiddenTypes.Contains(name)) continue;
                types.Add(new
                {
                    name,
                    description = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : ""
                });
            }

        return Ok(types);
    }

    [HttpGet("{projectId}/work-item-type-fields")]
    public async Task<IActionResult> ListWorkItemTypeFields(string projectId, [FromQuery] string workItemType)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });
        if (string.IsNullOrWhiteSpace(workItemType)) return BadRequest(new { detail = "Work item type is required." });

        var http = BuildClient(pat);
        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/wit/workitemtypes/{Uri.EscapeDataString(workItemType)}/fields?api-version=7.1-preview.3";
        var resp = await GetWithRetry(http, url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { detail = "Failed to list fields." });

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var fields = new List<object>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var f in arr.EnumerateArray())
                fields.Add(new
                {
                    referenceName = f.TryGetProperty("referenceName", out var rn) ? rn.GetString() ?? "" : "",
                    name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : ""
                });

        return Ok(fields);
    }

    [HttpPost("{projectId}/templates/create")]
    public async Task<IActionResult> CreateSingleTemplate(string projectId, [FromBody] CreateTemplateRequest request)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });
        if (string.IsNullOrWhiteSpace(request.Team)) return BadRequest(new { detail = "Team is required." });
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { detail = "Template name is required." });
        if (string.IsNullOrWhiteSpace(request.WorkItemTypeName)) return BadRequest(new { detail = "Work item type is required." });

        var http = BuildClient(pat);
        var template = new SourceTemplate
        {
            Name = request.Name,
            Description = request.Description ?? "",
            WorkItemTypeName = request.WorkItemTypeName,
            Fields = request.Fields ?? new Dictionary<string, string>()
        };

        try
        {
            await CreateTemplate(http, project, request.Team, template);
            return Ok(new { detail = "Template created successfully." });
        }
        catch (MissingFieldException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (AzureDevOpsPatScopeException ex)
        {
            return StatusCode(502, new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(502, new { detail = ex.Message });
        }
    }

    [HttpDelete("{projectId}/templates/{templateId}")]
    public async Task<IActionResult> DeleteSingleTemplate(string projectId, string templateId, [FromQuery] string team)
    {
        var (pat, project) = Resolve(projectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (project is null) return NotFound(new { detail = "Project not found." });
        if (string.IsNullOrWhiteSpace(team)) return BadRequest(new { detail = "Team is required." });

        var http = BuildClient(pat);

        try
        {
            await DeleteTemplate(http, project, team, templateId);
            return Ok(new { detail = "Template deleted successfully." });
        }
        catch (AzureDevOpsPatScopeException ex)
        {
            return StatusCode(502, new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(502, new { detail = ex.Message });
        }
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] TemplateManagerRequest request)
    {
        var (pat, sourceProject) = Resolve(request.SourceProjectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (sourceProject is null) return NotFound(new { detail = "Source project not found." });
        if (request.TemplateIds.Count == 0) return BadRequest(new { detail = "No templates selected." });
        if (request.Targets.Count == 0) return BadRequest(new { detail = "No target projects selected." });

        var http = BuildClient(pat);

        var sourceTemplates = await FetchFullTemplates(http, sourceProject, request.SourceTeam, request.TemplateIds);
        if (sourceTemplates.Count == 0)
            return BadRequest(new { detail = "None of the selected templates could be found in the source project." });

        // Collect custom fields per work item type from source templates
        var customFieldsByWit = sourceTemplates
            .GroupBy(t => t.WorkItemTypeName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(t => t.Fields.Keys)
                    .Where(f => f.StartsWith("Custom.", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var actions = new List<object>();
        var missingFieldsPerTarget = new Dictionary<string, List<string>>();
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = new List<Task>();

        foreach (var target in request.Targets)
        {
            var targetProject = configStore.GetProject(target.ProjectId);
            if (targetProject is null) continue;

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var existingTemplates = await FetchTemplateList(http, targetProject, target.Team);
                    var existingNames = existingTemplates
                        .Select(t => $"{t.name}|{t.workItemTypeName}".ToLowerInvariant())
                        .ToHashSet();

                    // Check which custom fields are missing per work item type
                    var missingFields = new List<string>();
                    foreach (var (wit, customFields) in customFieldsByWit)
                    {
                        if (customFields.Count == 0) continue;
                        var witFields = await FetchWorkItemTypeFields(http, targetProject, wit);
                        foreach (var f in customFields)
                            if (!witFields.Contains(f))
                                missingFields.Add(f);
                    }

                    missingFields = missingFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                    lock (missingFieldsPerTarget)
                    {
                        if (missingFields.Count > 0)
                            missingFieldsPerTarget[targetProject.Project] = missingFields;
                    }

                    foreach (var src in sourceTemplates)
                    {
                        var key = $"{src.Name}|{src.WorkItemTypeName}".ToLowerInvariant();
                        var status = existingNames.Contains(key) ? "already_exists" : "will_create";

                        lock (actions)
                        {
                            actions.Add(new
                            {
                                template_name = src.Name,
                                work_item_type = src.WorkItemTypeName,
                                target_project = targetProject.Project,
                                target_team = target.Team,
                                status
                            });
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Build a deduplicated list of all missing fields across all targets
        var allMissingFields = missingFieldsPerTarget
            .SelectMany(kv => kv.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f)
            .ToList();

        return Ok(new { actions, missing_fields = allMissingFields, missing_fields_per_target = missingFieldsPerTarget });
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] TemplateManagerRequest request)
    {
        var (pat, sourceProject) = Resolve(request.SourceProjectId);
        if (pat is null) return BadRequest(new { detail = "PAT not configured." });
        if (sourceProject is null) return NotFound(new { detail = "Source project not found." });
        if (request.TemplateIds.Count == 0) return BadRequest(new { detail = "No templates selected." });
        if (request.Targets.Count == 0) return BadRequest(new { detail = "No target projects selected." });

        var http = BuildClient(pat);

        List<SourceTemplate> sourceTemplates;
        // Operation-level retry: absorb transient 401s silently (up to 2 extra attempts, 3s apart)
        for (var opAttempt = 0; ; opAttempt++)
        {
            try
            {
                sourceTemplates = await FetchFullTemplates(http, sourceProject, request.SourceTeam, request.TemplateIds);
                break;
            }
            catch (AzureDevOpsPatScopeException) when (opAttempt < 2)
            {
                await Task.Delay(3000);
            }
            catch (AzureDevOpsPatScopeException ex)
            {
                return StatusCode(502, new { detail = $"Failed to read source templates after multiple retries: {ex.Message}" });
            }
        }
        if (sourceTemplates.Count == 0)
            return BadRequest(new { detail = "None of the selected templates could be found in the source project." });

        var results = new List<object>();
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = new List<Task>();

        foreach (var target in request.Targets)
        {
            var targetProject = configStore.GetProject(target.ProjectId);
            if (targetProject is null) continue;

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    List<(string id, string name, string workItemTypeName)> existingTemplates;
                    // Operation-level retry: if FetchTemplateList fails after per-call retries, wait and try once more
                    for (var opAttempt = 0; ; opAttempt++)
                    {
                        try
                        {
                            existingTemplates = await FetchTemplateList(http, targetProject, target.Team);
                            break;
                        }
                        catch (Exception) when (opAttempt < 1)
                        {
                            await Task.Delay(3000);
                        }
                        catch (Exception ex)
                        {
                            // All retries exhausted — record error for this target
                            lock (results)
                            {
                                foreach (var src in sourceTemplates)
                                {
                                    results.Add(new
                                    {
                                        template_name = src.Name,
                                        target_project = targetProject.Project,
                                        target_team = target.Team,
                                        status = "error",
                                        detail = $"Failed to list existing templates: {ex.Message}"
                                    });
                                }
                            }
                            return;
                        }
                    }

                    var existingByKey = existingTemplates
                        .GroupBy(t => $"{t.name}|{t.workItemTypeName}".ToLowerInvariant())
                        .ToDictionary(g => g.Key, g => g.First().id);

                    // Fetch target fields per work item type to detect missing custom fields
                    var witFieldsCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                    foreach (var src in sourceTemplates)
                    {
                        // Check if this template uses custom fields missing in the target
                        var customFields = src.Fields.Keys
                            .Where(f => f.StartsWith("Custom.", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (customFields.Count > 0)
                        {
                            if (!witFieldsCache.TryGetValue(src.WorkItemTypeName, out var targetWitFields))
                            {
                                targetWitFields = await FetchWorkItemTypeFields(http, targetProject, src.WorkItemTypeName);
                                witFieldsCache[src.WorkItemTypeName] = targetWitFields;
                            }
                            var missingFields = customFields
                                .Where(f => !targetWitFields.Contains(f))
                                .ToList();
                            if (missingFields.Count > 0)
                            {
                                lock (results)
                                {
                                    results.Add(new
                                    {
                                        template_name = src.Name,
                                        target_project = targetProject.Project,
                                        target_team = target.Team,
                                        status = "skipped",
                                        detail = $"Missing custom field(s) in target: {string.Join(", ", missingFields)}"
                                    });
                                }
                                continue;
                            }
                        }

                        var key = $"{src.Name}|{src.WorkItemTypeName}".ToLowerInvariant();

                        if (existingByKey.TryGetValue(key, out var existingId))
                        {
                            if (request.OverwriteExisting)
                            {
                                // Delete the existing template, then create new one
                                try
                                {
                                    await DeleteTemplate(http, targetProject, target.Team, existingId);
                                    await CreateTemplate(http, targetProject, target.Team, src);
                                    lock (results)
                                    {
                                        results.Add(new
                                        {
                                            template_name = src.Name,
                                            target_project = targetProject.Project,
                                            target_team = target.Team,
                                            status = "overwritten",
                                            detail = "Overwritten"
                                        });
                                    }
                                }
                                catch (MissingFieldException ex)
                                {
                                    lock (results)
                                    {
                                        results.Add(new
                                        {
                                            template_name = src.Name,
                                            target_project = targetProject.Project,
                                            target_team = target.Team,
                                            status = "skipped",
                                            detail = ex.Message
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    lock (results)
                                    {
                                        results.Add(new
                                        {
                                            template_name = src.Name,
                                            target_project = targetProject.Project,
                                            target_team = target.Team,
                                            status = "error",
                                            detail = ex.Message
                                        });
                                    }
                                }
                            }
                            else
                            {
                                // Create alongside (no skip)
                                try
                                {
                                    await CreateTemplate(http, targetProject, target.Team, src);
                                    lock (results)
                                    {
                                        results.Add(new
                                        {
                                            template_name = src.Name,
                                            target_project = targetProject.Project,
                                            target_team = target.Team,
                                            status = "created",
                                            detail = "Created alongside existing"
                                        });
                                    }
                                }
                                catch (MissingFieldException ex)
                                {
                                    lock (results)
                                    {
                                        results.Add(new
                                        {
                                            template_name = src.Name,
                                            target_project = targetProject.Project,
                                            target_team = target.Team,
                                            status = "skipped",
                                            detail = ex.Message
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    lock (results)
                                    {
                                        results.Add(new
                                        {
                                            template_name = src.Name,
                                            target_project = targetProject.Project,
                                            target_team = target.Team,
                                            status = "error",
                                            detail = ex.Message
                                        });
                                    }
                                }
                            }
                            continue;
                        }

                        try
                        {
                            await CreateTemplate(http, targetProject, target.Team, src);
                            lock (results)
                            {
                                results.Add(new
                                {
                                    template_name = src.Name,
                                    target_project = targetProject.Project,
                                    target_team = target.Team,
                                    status = "created",
                                    detail = ""
                                });
                            }
                        }
                        catch (MissingFieldException ex)
                        {
                            lock (results)
                            {
                                results.Add(new
                                {
                                    template_name = src.Name,
                                    target_project = targetProject.Project,
                                    target_team = target.Team,
                                    status = "skipped",
                                    detail = ex.Message
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (results)
                            {
                                results.Add(new
                                {
                                    template_name = src.Name,
                                    target_project = targetProject.Project,
                                    target_team = target.Team,
                                    status = "error",
                                    detail = ex.Message
                                });
                            }
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        return Ok(new { results });
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

    /// <summary>
    /// Sends an HTTP request with a single retry on transient 401 responses.
    /// Azure DevOps occasionally returns 401 even with valid PATs.
    /// Retries up to 3 times with exponential backoff (1s, 2s, 4s).
    /// </summary>
    private static async Task<HttpResponseMessage> SendWithRetry(HttpClient http, HttpRequestMessage request)
    {
        // Read body upfront so we can recreate the request on retries
        string? bodyText = null;
        if (request.Content is not null)
            bodyText = await request.Content.ReadAsStringAsync();

        var resp = await http.SendAsync(request);
        for (var retry = 1; retry <= 3 && resp.StatusCode == System.Net.HttpStatusCode.Unauthorized; retry++)
        {
            await Task.Delay(1000 * (1 << (retry - 1))); // 1s, 2s, 4s
            var retryReq = new HttpRequestMessage(request.Method, request.RequestUri);
            if (bodyText is not null)
                retryReq.Content = new StringContent(bodyText, Encoding.UTF8, "application/json");
            resp = await http.SendAsync(retryReq);
        }
        return resp;
    }

    private static async Task<HttpResponseMessage> GetWithRetry(HttpClient http, string url)
    {
        var resp = await http.GetAsync(url);
        for (var retry = 1; retry <= 3 && resp.StatusCode == System.Net.HttpStatusCode.Unauthorized; retry++)
        {
            await Task.Delay(1000 * (1 << (retry - 1))); // 1s, 2s, 4s
            resp = await http.GetAsync(url);
        }
        return resp;
    }

    private static async Task<List<(string id, string name, string workItemTypeName)>> FetchTemplateList(
        HttpClient http, ProjectConfig project, string team)
    {
        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/{Uri.EscapeDataString(team)}/_apis/wit/templates?api-version=7.1-preview.1";
        var resp = await GetWithRetry(http, url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return [];

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var list = new List<(string id, string name, string workItemTypeName)>();
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var t in arr.EnumerateArray())
            {
                var id = t.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                var name = t.GetProperty("name").GetString() ?? "";
                var wit = t.TryGetProperty("workItemTypeName", out var w) ? w.GetString() ?? "" : "";
                list.Add((id, name, wit));
            }

        return list;
    }

    private static async Task<List<SourceTemplate>> FetchFullTemplates(
        HttpClient http, ProjectConfig project, string team, List<string> templateIds)
    {
        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var result = new List<SourceTemplate>();

        foreach (var id in templateIds)
        {
            var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/{Uri.EscapeDataString(team)}/_apis/wit/templates/{Uri.EscapeDataString(id)}?api-version=7.1-preview.1";
            var resp = await GetWithRetry(http, url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode) continue;

            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var root = doc.RootElement;

            var name = root.GetProperty("name").GetString() ?? "";
            var description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var workItemTypeName = root.TryGetProperty("workItemTypeName", out var w) ? w.GetString() ?? "" : "";

            var fields = new Dictionary<string, string>();
            if (root.TryGetProperty("fields", out var fieldsEl))
                foreach (var prop in fieldsEl.EnumerateObject())
                    fields[prop.Name] = prop.Value.GetString() ?? "";

            result.Add(new SourceTemplate
            {
                Id = id,
                Name = name,
                Description = description,
                WorkItemTypeName = workItemTypeName,
                Fields = fields
            });
        }

        return result;
    }

    private static async Task<HashSet<string>> FetchWorkItemTypeFields(
        HttpClient http, ProjectConfig project, string workItemTypeName)
    {
        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/_apis/wit/workitemtypes/{Uri.EscapeDataString(workItemTypeName)}/fields?api-version=7.1-preview.3";
        var resp = await GetWithRetry(http, url);
        if (!resp.IsSuccessStatusCode) return [];

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.TryGetProperty("value", out var arr))
            foreach (var f in arr.EnumerateArray())
                if (f.TryGetProperty("referenceName", out var rn))
                    fields.Add(rn.GetString() ?? "");

        return fields;
    }

    private static async Task DeleteTemplate(HttpClient http, ProjectConfig project, string team, string templateId)
    {
        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/{Uri.EscapeDataString(team)}/_apis/wit/templates/{Uri.EscapeDataString(templateId)}?api-version=7.1-preview.1";
        var req = new HttpRequestMessage(HttpMethod.Delete, url);
        var resp = await SendWithRetry(http, req);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to delete template: {resp.StatusCode} — {body}");
        }
    }

    private static async Task CreateTemplate(HttpClient http, ProjectConfig project, string team, SourceTemplate template)
    {
        var orgUrl = $"https://dev.azure.com/{project.Organization}";
        var url = $"{orgUrl}/{Uri.EscapeDataString(project.Project)}/{Uri.EscapeDataString(team)}/_apis/wit/templates?api-version=7.1-preview.1";

        var payload = JsonSerializer.Serialize(new
        {
            name = template.Name,
            description = template.Description,
            workItemTypeName = template.WorkItemTypeName,
            fields = template.Fields
        });

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        var resp = await SendWithRetry(http, req);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);

        if (resp.IsSuccessStatusCode)
            return;

        var body = await resp.Content.ReadAsStringAsync();

        // If a field doesn't exist in the target, report as missing-field error (don't strip and retry)
        var fieldMatch = System.Text.RegularExpressions.Regex.Match(body, @"Cannot find field ([^\."",]+\.[^\."",]+)");
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest && fieldMatch.Success)
        {
            throw new MissingFieldException($"Target project does not have required custom field: {fieldMatch.Groups[1].Value}");
        }

        throw new InvalidOperationException($"Failed to create template '{template.Name}': {resp.StatusCode} — {body}");
    }

    private class SourceTemplate
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string WorkItemTypeName { get; set; } = "";
        public Dictionary<string, string> Fields { get; set; } = new();
    }
}

public class TemplateManagerRequest
{
    [JsonPropertyName("source_project_id")]
    public string SourceProjectId { get; set; } = "";

    [JsonPropertyName("source_team")]
    public string SourceTeam { get; set; } = "";

    [JsonPropertyName("template_ids")]
    public List<string> TemplateIds { get; set; } = [];

    [JsonPropertyName("targets")]
    public List<TemplateManagerTarget> Targets { get; set; } = [];

    [JsonPropertyName("overwrite_existing")]
    public bool OverwriteExisting { get; set; }
}

public class TemplateManagerTarget
{
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = "";

    [JsonPropertyName("team")]
    public string Team { get; set; } = "";
}

public class CreateTemplateRequest
{
    [JsonPropertyName("team")]
    public string Team { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("work_item_type_name")]
    public string WorkItemTypeName { get; set; } = "";

    [JsonPropertyName("fields")]
    public Dictionary<string, string>? Fields { get; set; }
}
