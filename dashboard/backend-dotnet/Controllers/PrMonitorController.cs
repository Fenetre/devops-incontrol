using System.Text.Json;
using System.Text.RegularExpressions;
using DashboardApi.Models;
using DashboardApi.Services;
using DashboardApi.Services.Checks;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/pr-monitor")]
public partial class PrMonitorController(ConfigStore configStore, ILogger<PrMonitorController> logger) : ControllerBase
{
    [GeneratedRegex(@"^[a-f0-9]{1,32}$")]
    private static partial Regex ProjectIdRegex();

    private static int? ComputeDaysInactive(JsonElement pr, List<JsonElement>? threads)
    {
        var isDraft = pr.TryGetProperty("isDraft", out var d) && d.GetBoolean();
        if (isDraft) return null;

        var now = DateTimeOffset.UtcNow;
        var creation = Helpers.ParseDevOpsDate(pr.TryGetProperty("creationDate", out var cd) ? cd.GetString() : "");
        var lastPush = creation;

        if (pr.TryGetProperty("lastMergeSourceCommit", out var mc) &&
            mc.TryGetProperty("committer", out var committer) &&
            committer.TryGetProperty("date", out var date))
        {
            var pushDate = Helpers.ParseDevOpsDate(date.GetString());
            if (pushDate > lastPush) lastPush = pushDate;
        }

        var lastActivity = lastPush;
        if (threads is not null)
        {
            foreach (var thread in threads)
            {
                if (!thread.TryGetProperty("comments", out var comments)) continue;
                foreach (var comment in comments.EnumerateArray())
                {
                    if (comment.TryGetProperty("publishedDate", out var pub))
                    {
                        var dt = Helpers.ParseDevOpsDate(pub.GetString());
                        if (dt > lastActivity) lastActivity = dt;
                    }
                }
            }
        }

        return (int)(now - lastActivity).TotalDays;
    }

    private static List<PrFlag> ComputeFlags(JsonElement pr, int staleDays,
        List<string> ignoreReviewers, int? daysInactive)
    {
        var flags = new List<PrFlag>();
        var reviewers = pr.TryGetProperty("reviewers", out var arr) ? arr.EnumerateArray().ToList() : [];

        // --- Unreviewed ---
        var ignoreLower = ignoreReviewers.Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.ToLowerInvariant()).ToList();
        var effectiveReviewers = reviewers;
        if (ignoreLower.Count > 0)
        {
            effectiveReviewers = reviewers.Where(r =>
            {
                var name = (r.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "").ToLowerInvariant();
                return !ignoreLower.Any(ig => name.Contains(ig));
            }).ToList();
        }
        var isDraft = pr.TryGetProperty("isDraft", out var d) && d.GetBoolean();
        if (!isDraft && effectiveReviewers.Count == 0)
            flags.Add(new PrFlag { Key = "unreviewed", Label = "No reviewers", Severity = "error" });

        // --- Approval ready ---
        if (reviewers.Count > 0)
        {
            var hasRejections = reviewers.Any(r => r.TryGetProperty("vote", out var v) && v.GetInt32() < 0);
            if (!hasRejections)
            {
                var ilseReviewers = reviewers.Where(r => Helpers.IsIlse(r)).ToList();
                var otherReviewers = reviewers.Where(r => !Helpers.IsIlse(r)).ToList();
                var requiredReviewers = reviewers.Where(r =>
                    r.TryGetProperty("isRequired", out var rq) && rq.GetBoolean()).ToList();

                var allRequiredOk = requiredReviewers.Count == 0 || requiredReviewers.All(r =>
                    Helpers.IsApproved(r.TryGetProperty("vote", out var v) ? v.GetInt32() : 0));

                if (allRequiredOk)
                {
                    bool approvalsMet;
                    if (ilseReviewers.Count > 0)
                    {
                        var ilseOk = ilseReviewers.Any(r =>
                            Helpers.IsApproved(r.TryGetProperty("vote", out var v) ? v.GetInt32() : 0));
                        var otherOk = otherReviewers.Count(r =>
                            Helpers.IsApproved(r.TryGetProperty("vote", out var v) ? v.GetInt32() : 0)) >= 1;
                        approvalsMet = ilseOk && otherOk;
                    }
                    else
                    {
                        approvalsMet = reviewers.Count(r =>
                            Helpers.IsApproved(r.TryGetProperty("vote", out var v) ? v.GetInt32() : 0)) >= 1;
                    }

                    if (approvalsMet)
                        flags.Add(new PrFlag { Key = "approval_ready", Label = "Ready to complete", Severity = "info" });
                }
            }
        }

        // --- Stale (skip drafts — stale counter starts after leaving draft) ---
        if (staleDays > 0 && daysInactive.HasValue && daysInactive.Value >= staleDays)
        {
            flags.Add(new PrFlag { Key = "stale", Label = $"{daysInactive.Value}d inactive", Severity = "warning" });
        }

        // --- Rejected ---
        var rejected = reviewers.Where(r => r.TryGetProperty("vote", out var v) && v.GetInt32() < 0).ToList();
        if (rejected.Count > 0)
        {
            var names = string.Join(", ", rejected.Take(3).Select(r =>
                r.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "?" : "?"));
            flags.Add(new PrFlag { Key = "rejected", Label = $"Rejected by {names}", Severity = "error" });
        }

        return flags;
    }

    private async Task<PrProjectResponse> FetchProjectPrsAsync(Models.ProjectConfig project, string pat)
    {
        var prChecks = project.Checks
            .Where(c => c.Enabled && c.CheckType is "pr_approval_check" or "stale_pr_check" or "unreviewed_pr_check")
            .ToDictionary(c => c.CheckType);

        var checksEnabled = prChecks.Count > 0;
        var repository = prChecks.Values.FirstOrDefault(c => !string.IsNullOrEmpty(c.Repository))?.Repository ?? "";
        var staleDays = prChecks.TryGetValue("stale_pr_check", out var sc) ? sc.StaleDays : 14;
        if (staleDays == 0) staleDays = 14;
        var ignoreReviewers = prChecks.TryGetValue("unreviewed_pr_check", out var uc) ? uc.IgnoreReviewers : [];

        var client = new AzureDevOpsClient(project.Organization, project.Project, pat);
        var prs = await client.GetActivePullRequestsAsync(repository);

        Dictionary<int, List<JsonElement>>? threadsMap = null;
        if (prs.Count > 0)
            threadsMap = await client.GetPullRequestThreadsBatchAsync(prs);

        var items = prs.Select(pr =>
        {
            var prId = pr.GetProperty("pullRequestId").GetInt32();
            var repoName = pr.TryGetProperty("repository", out var repo) && repo.TryGetProperty("name", out var n)
                ? n.GetString() ?? "" : "";
            var reviewerCount = pr.TryGetProperty("reviewers", out var revs) ? revs.GetArrayLength() : 0;
            var prThreads = threadsMap?.GetValueOrDefault(prId);
            var daysInactive = ComputeDaysInactive(pr, prThreads);

            return new PrItem
            {
                PrId = prId,
                Title = pr.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                Url = Helpers.PrUrl(project.Organization, project.Project, repoName, prId),
                Repository = repoName,
                CreatedBy = pr.TryGetProperty("createdBy", out var cb) && cb.TryGetProperty("displayName", out var dn)
                    ? dn.GetString() ?? "" : "",
                CreationDate = pr.TryGetProperty("creationDate", out var cd) ? cd.GetString() ?? "" : "",
                IsDraft = pr.TryGetProperty("isDraft", out var draft) && draft.GetBoolean(),
                ReviewerCount = reviewerCount,
                Reviewers = (pr.TryGetProperty("reviewers", out var revList) ? revList.EnumerateArray().ToList() : [])
                    .Select(r => new PrReviewer
                    {
                        Name = r.TryGetProperty("displayName", out var rdn) ? rdn.GetString() ?? "" : "",
                        Vote = r.TryGetProperty("vote", out var rv) ? rv.GetInt32() : 0,
                        IsRequired = r.TryGetProperty("isRequired", out var rq) && rq.GetBoolean(),
                    }).ToList(),
                DaysInactive = daysInactive,
                Flags = checksEnabled ? ComputeFlags(pr, staleDays, ignoreReviewers, daysInactive) : [],
            };
        }).ToList();

        return new PrProjectResponse
        {
            ProjectId = project.Id,
            ProjectName = project.Project,
            Organization = project.Organization,
            Prs = items,
        };
    }

    [HttpGet]
    public async Task<List<PrProjectResponse>> ListAllPrProjects(CancellationToken cancellationToken = default)
    {
        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            throw new BadHttpRequestException("PAT not configured.");

        var projects = configStore.ListProjects();

        if (projects.Count == 0) return [];

        var throttle = new SemaphoreSlim(6);
        var tasks = projects.Select(async proj =>
        {
            await throttle.WaitAsync();
            try { return await FetchProjectPrsAsync(proj, pat); }
            catch (AzureDevOpsPatScopeException ex)
            {
                logger.LogWarning(ex, "PAT scope error in PR monitor for {Project}", proj.Project);
                return new PrProjectResponse
                {
                    ProjectId = proj.Id,
                    ProjectName = proj.Project,
                    Organization = proj.Organization,
                    Error = ex.Message,
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PR monitor failed for {Project}", proj.Project);
                return new PrProjectResponse
                {
                    ProjectId = proj.Id,
                    ProjectName = proj.Project,
                    Organization = proj.Organization,
                    Error = "Failed to fetch PR data — see server logs.",
                };
            }
            finally { throttle.Release(); }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    [HttpGet("{projectId}")]
    public async Task<IActionResult> GetProjectPrs(string projectId, CancellationToken cancellationToken = default)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            return BadRequest(new { detail = "PAT not configured." });

        var project = configStore.GetProject(projectId);
        if (project is null) return NotFound(new { detail = "Project not found." });

        try
        {
            var result = await FetchProjectPrsAsync(project, pat);
            return Ok(result);
        }
        catch (AzureDevOpsPatScopeException ex)
        {
            logger.LogWarning(ex, "PAT scope error in PR monitor for {Project}", project.Project);
            return Ok(new PrProjectResponse
            {
                ProjectId = project.Id,
                ProjectName = project.Project,
                Organization = project.Organization,
                Error = ex.Message,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PR monitor failed for {Project}", project.Project);
            return Ok(new PrProjectResponse
            {
                ProjectId = project.Id,
                ProjectName = project.Project,
                Organization = project.Organization,
                Error = "Failed to fetch PR data — see server logs.",
            });
        }
    }
}
