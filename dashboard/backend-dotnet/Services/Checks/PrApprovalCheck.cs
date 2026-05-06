using System.Text.Json;

namespace DashboardApi.Services.Checks;

public class PrApprovalCheck : ICheckCase
{
    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat, config.Repository));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat, string repository)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);
        var prs = await client.GetActivePullRequestsAsync(repository);

        var flagged = new List<FlaggedItem>();
        foreach (var pr in prs)
        {
            if (!pr.TryGetProperty("reviewers", out var reviewersArr)) continue;
            var reviewers = reviewersArr.EnumerateArray().ToList();
            if (reviewers.Count == 0) continue;

            // Skip PRs with rejections
            if (reviewers.Any(r => r.TryGetProperty("vote", out var v) && v.GetInt32() < 0))
                continue;

            var ilseReviewers = reviewers.Where(r => Helpers.IsIlse(r)).ToList();
            var otherReviewers = reviewers.Where(r => !Helpers.IsIlse(r)).ToList();

            // All required reviewers must have approved
            var requiredReviewers = reviewers.Where(r =>
                r.TryGetProperty("isRequired", out var req) && req.GetBoolean()).ToList();
            if (requiredReviewers.Count > 0)
            {
                var allRequiredApproved = requiredReviewers.All(r =>
                    Helpers.IsApproved(r.TryGetProperty("vote", out var v) ? v.GetInt32() : 0));
                if (!allRequiredApproved) continue;
            }

            bool allApprovalsMet;
            if (ilseReviewers.Count > 0)
            {
                var ilseApproved = ilseReviewers.Any(r =>
                    Helpers.IsApproved(r.TryGetProperty("vote", out var v) ? v.GetInt32() : 0));
                var otherApprovedCount = otherReviewers.Count(r =>
                    Helpers.IsApproved(r.TryGetProperty("vote", out var v) ? v.GetInt32() : 0));
                allApprovalsMet = ilseApproved && otherApprovedCount >= 1;
            }
            else
            {
                var approvedCount = reviewers.Count(r =>
                    Helpers.IsApproved(r.TryGetProperty("vote", out var v) ? v.GetInt32() : 0));
                allApprovalsMet = approvedCount >= 1;
            }

            if (allApprovalsMet)
            {
                var prId = pr.GetProperty("pullRequestId").GetInt32();
                var title = pr.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var repoName = pr.TryGetProperty("repository", out var repo) && repo.TryGetProperty("name", out var n)
                    ? n.GetString() ?? "" : "";
                var createdBy = pr.TryGetProperty("createdBy", out var cb) && cb.TryGetProperty("displayName", out var dn)
                    ? dn.GetString() ?? "" : "";

                flagged.Add(new FlaggedItem
                {
                    Id = prId,
                    Title = title,
                    Url = Helpers.PrUrl(projectCfg.Organization, projectCfg.Project, repoName, prId),
                    WorkItemType = $"PR ({repoName})",
                    Project = projectCfg.Project,
                    AssignedTo = createdBy,
                });
            }
        }

        return new CaseResult
        {
            Header = $"PR Approval Check — {projectCfg.Project}",
            FlaggedItems = flagged,
        };
    }
}
