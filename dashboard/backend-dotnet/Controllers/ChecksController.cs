using System.Text.RegularExpressions;
using System.Net;
using System.Net.Mail;
using System.Text;
using DashboardApi.Models;
using DashboardApi.Services;
using DashboardApi.Services.Checks;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/checks")]
public partial class ChecksController(ConfigStore configStore, ILogger<ChecksController> logger) : ControllerBase
{
    [GeneratedRegex(@"^[a-f0-9]{1,32}$")]
    private static partial Regex ProjectIdRegex();

    private static readonly object ResultLock = new();
    private static RunResultResponse? LastResult;
    private static readonly HashSet<string> InfoOnlyChecks = ["tag_overview_check"];

    private static bool AllowInsecureSmtp()
        => string.Equals(
            Environment.GetEnvironmentVariable("SMTP_ALLOW_INSECURE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private CaseConfig BuildCaseConfig(ProjectConfig project, string checkType)
    {
        var checkCfg = project.Checks.FirstOrDefault(c => c.CheckType == checkType && c.Enabled);
        var wiql = checkCfg?.CustomWiql ?? "";
        if (string.IsNullOrEmpty(wiql))
            wiql = ConfigStore.DefaultWiql.GetValueOrDefault(checkType, "unused");

        var excludeTypes = checkCfg?.ExcludeTypes ?? [];
        var apiVersion = checkCfg?.ApiVersion ?? "7.1";
        var repository = checkCfg?.Repository ?? "";
        var staleDays = checkCfg?.StaleDays ?? 14;
        var ignoreReviewers = checkCfg?.IgnoreReviewers ?? [];
        var estimateMode = checkCfg?.EstimateMode ?? "both";

        return new CaseConfig
        {
            CaseType = checkType,
            Projects = [new Services.Checks.DevOpsProjectConfig
            {
                Organization = project.Organization,
                Project = project.Project,
                ApiVersion = apiVersion,
                Wiql = wiql,
                AreaPath = project.AreaPath,
                IgnoreTitleContains = project.IgnoreTitleContains,
                IgnoreParentTitleContains = project.IgnoreParentTitleContains,
            }],
            ExcludeTypes = excludeTypes,
            Repository = repository,
            StaleDays = staleDays,
            IgnoreReviewers = ignoreReviewers,
            EstimateMode = estimateMode,
        };
    }

    private static CheckResultResponse ConvertResult(CaseResult cr, string checkType, string projectId, string projectName)
        => new()
        {
            CheckType = checkType,
            CheckLabel = ProjectsController.GetCheckLabel(checkType),
            Header = cr.Header,
            ProjectId = projectId,
            ProjectName = projectName,
            FlaggedItems = cr.FlaggedItems.Select(f => new FlaggedItemResponse
            {
                Id = f.Id,
                Title = f.Title,
                Url = f.Url,
                WorkItemType = f.WorkItemType,
                Project = f.Project,
                AssignedTo = f.AssignedTo,
                AssignedToEmail = f.AssignedToEmail,
                CreatedDate = f.CreatedDate,
                IterationPath = f.IterationPath,
                StoryPoints = f.StoryPoints,
                State = f.State,
            }).ToList(),
        };

    private async Task<ProjectResultResponse> RunChecksForProjectAsync(ProjectConfig project, string pat)
    {
        var enabled = project.Checks.Where(c => c.Enabled).ToList();
        var checks = new List<CheckResultResponse>();

        if (enabled.Count <= 1)
        {
            foreach (var cfg in enabled)
            {
                var results = await RunSingleCheckAsync(cfg, project, pat);
                checks.AddRange(results);
            }
        }
        else
        {
            var tasks = enabled.Select(cfg => RunSingleCheckAsync(cfg, project, pat));
            var allResults = await Task.WhenAll(tasks);
            checks = allResults.SelectMany(r => r).ToList();
        }

        var total = checks.Where(c => !InfoOnlyChecks.Contains(c.CheckType))
            .Sum(c => c.FlaggedItems.Count);

        return new ProjectResultResponse
        {
            ProjectId = project.Id,
            ProjectName = project.Project,
            Organization = project.Organization,
            Checks = checks,
            TotalIssues = total,
        };
    }

    private async Task<List<CheckResultResponse>> RunSingleCheckAsync(ProjectCheckConfig checkCfg, ProjectConfig project, string pat)
    {
        var caseInstance = CheckRegistry.GetCase(checkCfg.CheckType);
        if (caseInstance is null) return [];

        try
        {
            var caseConfig = BuildCaseConfig(project, checkCfg.CheckType);
            var results = await caseInstance.RunAsync(caseConfig, pat);
            return results.Select(cr => ConvertResult(cr, checkCfg.CheckType, project.Id, project.Project)).ToList();
        }
        catch (AzureDevOpsPatScopeException ex)
        {
            logger.LogWarning(ex, "PAT scope error for check {CheckType} on project {Project}", checkCfg.CheckType, project.Project);
            return [new CheckResultResponse
            {
                CheckType = checkCfg.CheckType,
                CheckLabel = ProjectsController.GetCheckLabel(checkCfg.CheckType),
                ProjectId = project.Id,
                ProjectName = project.Project,
                Error = ex.Message,
            }];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Check {CheckType} failed for project {Project}", checkCfg.CheckType, project.Project);
            return [new CheckResultResponse
            {
                CheckType = checkCfg.CheckType,
                CheckLabel = ProjectsController.GetCheckLabel(checkCfg.CheckType),
                ProjectId = project.Id,
                ProjectName = project.Project,
                Error = "Check failed — see server logs for details.",
            }];
        }
    }

    [HttpPost("run")]
    public async Task<RunResultResponse> RunAllChecks(CancellationToken cancellationToken = default)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            throw new BadHttpRequestException("PAT not configured. Set it in Settings first.");

        var projects = configStore.ListProjects();
        if (projects.Count == 0)
            throw new BadHttpRequestException("No projects configured.");

        AzureDevOpsClient.ClearRunCaches();

        var throttle = new SemaphoreSlim(6);
        var tasks = projects.Select(async proj =>
        {
            await throttle.WaitAsync();
            try { return await RunChecksForProjectAsync(proj, pat); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to run checks for project {Project}", proj.Project);
                return new ProjectResultResponse
                {
                    ProjectId = proj.Id,
                    ProjectName = proj.Project,
                    Organization = proj.Organization,
                };
            }
            finally { throttle.Release(); }
        });

        var projectResults = await Task.WhenAll(tasks);
        var total = projectResults.Sum(pr => pr.TotalIssues);
        var result = new RunResultResponse
        {
            Projects = projectResults.ToList(),
            TotalIssues = total,
            RanAt = DateTimeOffset.UtcNow.ToString("o"),
        };

        lock (ResultLock) { LastResult = result; }
        return result;
    }

    [HttpPost("run/{projectId}")]
    public async Task<IActionResult> RunChecksForProject(string projectId, CancellationToken cancellationToken = default)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured. Set it in Settings first." });

        var project = configStore.GetProject(projectId);
        if (project is null) return NotFound(new { detail = "Project not found" });

        AzureDevOpsClient.ClearRunCaches();
        var pr = await RunChecksForProjectAsync(project, pat);

        var currentProjectIds = configStore.ListProjects().Select(p => p.Id).ToHashSet();
        lock (ResultLock)
        {
            LastResult ??= new RunResultResponse { RanAt = DateTimeOffset.UtcNow.ToString("o") };
            LastResult.Projects = LastResult.Projects
                .Where(p => p.ProjectId != projectId && currentProjectIds.Contains(p.ProjectId))
                .Append(pr).ToList();
            LastResult.TotalIssues = LastResult.Projects.Sum(p => p.TotalIssues);
            LastResult.RanAt = DateTimeOffset.UtcNow.ToString("o");
        }

        return Ok(pr);
    }

    [HttpGet("results")]
    public RunResultResponse GetCachedResults()
    {
        var currentProjectIds = configStore.ListProjects().Select(p => p.Id).ToHashSet();
        lock (ResultLock)
        {
            if (LastResult is null) return new RunResultResponse();
            LastResult.Projects = LastResult.Projects
                .Where(p => currentProjectIds.Contains(p.ProjectId)).ToList();
            LastResult.TotalIssues = LastResult.Projects.Sum(p => p.TotalIssues);
            return LastResult;
        }
    }

    [HttpGet("tag-items/{projectId}")]
    public async Task<IActionResult> GetTagWorkItems(string projectId, [FromQuery] string tag, CancellationToken cancellationToken = default)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });
        if (string.IsNullOrWhiteSpace(tag) || tag.Length > 256)
            return BadRequest(new { detail = "Invalid tag." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var project = configStore.GetProject(projectId);
        if (project is null) return NotFound(new { detail = "Project not found" });

        var items = await FetchTagWorkItemsAsync(project, pat, tag);
        return Ok(new TagWorkItemsResponse { Tag = tag, Items = items });
    }

    [HttpGet("tags/{projectId}")]
    public async Task<IActionResult> GetProjectTags(string projectId, CancellationToken cancellationToken = default)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var project = configStore.GetProject(projectId);
        if (project is null) return NotFound(new { detail = "Project not found" });

        var tags = await new AzureDevOpsClient(project.Organization, project.Project, pat).GetTagsAsync();
        return Ok(new { tags });
    }

    [HttpPost("tag-remove/{projectId}")]
    public async Task<IActionResult> RemoveTagFromWorkItems(string projectId, [FromBody] TagOperationRequest body, CancellationToken cancellationToken = default)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });
        if (string.IsNullOrWhiteSpace(body.Tag) || body.Tag.Length > 256)
            return BadRequest(new { detail = "Invalid tag." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var project = configStore.GetProject(projectId);
        if (project is null) return NotFound(new { detail = "Project not found" });

        try
        {
            var updated = await UpdateTagOnWorkItemsAsync(project, pat, body.Tag, null, body.WorkItemId);
            return Ok(new TagOperationResponse { Ok = true, Updated = updated });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
    }

    [HttpPost("tag-rename/{projectId}")]
    public async Task<IActionResult> RenameTagOnWorkItems(string projectId, [FromBody] TagOperationRequest body, CancellationToken cancellationToken = default)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });
        if (string.IsNullOrWhiteSpace(body.Tag) || body.Tag.Length > 256)
            return BadRequest(new { detail = "Invalid tag." });
        if (string.IsNullOrWhiteSpace(body.NewTag) || body.NewTag.Length > 256)
            return BadRequest(new { detail = "Invalid new tag." });
        if (body.Tag == body.NewTag)
            return BadRequest(new { detail = "New tag must be different from the current tag." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var project = configStore.GetProject(projectId);
        if (project is null) return NotFound(new { detail = "Project not found" });

        try
        {
            var updated = await UpdateTagOnWorkItemsAsync(project, pat, body.Tag, body.NewTag, body.WorkItemId);
            return Ok(new TagOperationResponse { Ok = true, Updated = updated });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { detail = ex.Message });
        }
    }

    /// <summary>Remove a tag (newTag=null) or replace it with newTag. If workItemId is set, only that item.</summary>
    private static async Task<int> UpdateTagOnWorkItemsAsync(ProjectConfig project, string pat, string tag, string? newTag, int? workItemId = null)
    {
        var client = new AzureDevOpsClient(project.Organization, project.Project, pat);

        List<int> ids;
        if (workItemId.HasValue)
        {
            ids = [workItemId.Value];
        }
        else
        {
            var escapedTag = Helpers.EscapeWiql(tag);
            var areaFilter = !string.IsNullOrEmpty(project.AreaPath)
                ? $" AND [System.AreaPath] UNDER '{Helpers.EscapeWiql(project.AreaPath)}'" : "";

            var wiql =
                "SELECT [System.Id] FROM WorkItems " +
                $"WHERE [System.TeamProject] = '{Helpers.EscapeWiql(project.Project)}'" +
                areaFilter +
                $" AND [System.Tags] CONTAINS '{escapedTag}'";

            ids = await client.RunWiqlAsync(wiql);
        }
        if (ids.Count == 0) return 0;

        var rawItems = await client.GetWorkItemsAsync(ids, fields: ["System.Tags"]);
        var updated = 0;
        foreach (var item in rawItems)
        {
            var itemId = item.GetProperty("id").GetInt32();
            var tags = Helpers.GetTags(item);

            var filtered = tags.Where(t => !t.Equals(tag, StringComparison.OrdinalIgnoreCase)).ToList();
            if (newTag != null && !filtered.Any(t => t.Equals(newTag, StringComparison.OrdinalIgnoreCase)))
                filtered.Add(newTag);

            var newTagsStr = string.Join("; ", filtered);
            await client.UpdateWorkItemTagsAsync(itemId, newTagsStr);
            updated++;
        }
        return updated;
    }

    private static async Task<List<TagWorkItemResponse>> FetchTagWorkItemsAsync(ProjectConfig project, string pat, string tag)
    {
        var client = new AzureDevOpsClient(project.Organization, project.Project, pat);
        var escapedTag = Helpers.EscapeWiql(tag);
        var areaFilter = !string.IsNullOrEmpty(project.AreaPath)
            ? $" AND [System.AreaPath] UNDER '{Helpers.EscapeWiql(project.AreaPath)}'" : "";

        var wiql =
            "SELECT [System.Id] FROM WorkItems " +
            $"WHERE [System.TeamProject] = '{Helpers.EscapeWiql(project.Project)}'" +
            areaFilter +
            " AND [System.State] NOT IN ('Removed','Closed')" +
            $" AND [System.Tags] CONTAINS '{escapedTag}'";

        var ids = await client.RunWiqlAsync(wiql);
        if (ids.Count == 0) return [];

        var fields = new List<string> { "System.Title", "System.WorkItemType", "System.State", "System.AssignedTo" };
        var rawItems = await client.GetWorkItemsAsync(ids, fields: fields);

        return rawItems.Select(item =>
        {
            var itemId = item.GetProperty("id").GetInt32();
            return new TagWorkItemResponse
            {
                Id = itemId,
                Title = Helpers.GetField(item, "System.Title"),
                Url = Helpers.WorkItemUrl(project.Organization, project.Project, itemId),
                WorkItemType = Helpers.GetField(item, "System.WorkItemType"),
                State = Helpers.GetField(item, "System.State"),
                AssignedTo = Helpers.GetField(item, "System.AssignedTo"),
            };
        }).ToList();
    }

    [HttpPost("send-mail/{projectId}/{checkType}")]
    public IActionResult SendCheckMail(string projectId, string checkType, [FromBody] SendMailRequest body)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });

        var cfg = configStore.LoadConfig();
        if (string.IsNullOrEmpty(cfg.EmailFrom))
            return BadRequest(new SendMailResponse(false, "No FROM email address configured. Set it in Settings."));

        var to = body.To?.Trim();
        if (string.IsNullOrEmpty(to) || !to.Contains('@'))
            return BadRequest(new SendMailResponse(false, "Invalid recipient address."));

        // Find check results
        CheckResultResponse? checkResult = null;
        lock (ResultLock)
        {
            var proj = LastResult?.Projects.FirstOrDefault(p => p.ProjectId == projectId);
            checkResult = proj?.Checks.FirstOrDefault(c => c.CheckType == checkType);
        }
        if (checkResult is null || checkResult.FlaggedItems.Count == 0)
            return BadRequest(new SendMailResponse(false, "No check results to send."));

        // Optionally filter by assigned_to for per-person mails
        var items = checkResult.FlaggedItems;
        var assignedTo = body.AssignedTo?.Trim();
        if (!string.IsNullOrEmpty(assignedTo))
        {
            items = items.Where(i => i.AssignedTo.Equals(assignedTo, StringComparison.OrdinalIgnoreCase)).ToList();
            if (items.Count == 0)
                return BadRequest(new SendMailResponse(false, $"No items assigned to {assignedTo}."));
        }

        // Build HTML email body
        var personLabel = !string.IsNullOrEmpty(assignedTo) ? $" for {assignedTo}" : "";
        var subject = $"{checkResult.CheckLabel} — {checkResult.ProjectName}{personLabel}";
        var greeting = !string.IsNullOrEmpty(assignedTo)
            ? $"<p>Hi {WebUtility.HtmlEncode(assignedTo.Split(' ')[0])},</p><p>This is a friendly reminder that the following work items are missing estimates. Could you please add them?</p>"
            : "";
        var html = new StringBuilder();
        html.Append("<html><body style=\"font-family:Segoe UI,sans-serif;font-size:14px;color:#333\">");
        html.Append($"<h2>{WebUtility.HtmlEncode(subject)}</h2>");
        html.Append(greeting);
        html.Append($"<p>{items.Count} issue(s) found:</p>");
        html.Append("<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;border-color:#ddd\">");
        html.Append("<tr style=\"background:#f5f5f5\"><th>ID</th><th>Title</th><th>Type</th><th>Assigned To</th></tr>");
        foreach (var item in items)
        {
            html.Append("<tr>");
            html.Append($"<td><a href=\"{WebUtility.HtmlEncode(item.Url)}\">{item.Id}</a></td>");
            html.Append($"<td>{WebUtility.HtmlEncode(item.Title)}</td>");
            html.Append($"<td>{WebUtility.HtmlEncode(item.WorkItemType)}</td>");
            html.Append($"<td>{WebUtility.HtmlEncode(item.AssignedTo)}</td>");
            html.Append("</tr>");
        }
        html.Append("</table>");
        html.Append("<p style=\"color:#888;font-size:12px;margin-top:16px\">Sent by DevOps Backlog Monitor</p>");
        html.Append("</body></html>");

        try
        {
            using var message = new MailMessage(cfg.EmailFrom, to, subject, html.ToString());
            message.IsBodyHtml = true;

            using var client = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort);
            var allowInsecureSmtp = AllowInsecureSmtp();
            client.EnableSsl = !allowInsecureSmtp;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.Timeout = 15000;

            if (allowInsecureSmtp)
                logger.LogWarning("SMTP_ALLOW_INSECURE=true is enabled; SMTP transport is not enforced with TLS.");

            client.Send(message);

            logger.LogInformation("Mail sent for {CheckType}/{ProjectId} to {To}", checkType, projectId, to);
            return Ok(new SendMailResponse(true, $"Email sent to {to}"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send mail for {CheckType}/{ProjectId}", checkType, projectId);
            return StatusCode(500, new SendMailResponse(false, "Failed to send email. Check server logs for details."));
        }
    }
}
