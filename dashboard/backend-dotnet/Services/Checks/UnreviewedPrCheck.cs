namespace DashboardApi.Services.Checks;

public class UnreviewedPrCheck : ICheckCase
{
    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var ignoreReviewers = config.IgnoreReviewers ?? [];
        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat, config.Repository, ignoreReviewers));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat,
        string repository, List<string> ignoreReviewers)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);
        var prs = await client.GetActivePullRequestsAsync(repository);

        var ignoreLower = ignoreReviewers.Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.ToLowerInvariant()).ToList();

        var flagged = new List<FlaggedItem>();
        foreach (var pr in prs)
        {
            if (pr.TryGetProperty("isDraft", out var draft) && draft.GetBoolean()) continue;

            var reviewers = pr.TryGetProperty("reviewers", out var arr)
                ? arr.EnumerateArray().ToList() : [];

            if (ignoreLower.Count > 0)
            {
                reviewers = reviewers.Where(r =>
                {
                    var name = (r.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "").ToLowerInvariant();
                    return !ignoreLower.Any(ig => name.Contains(ig));
                }).ToList();
            }

            if (reviewers.Count == 0)
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
            Header = $"Unreviewed PR Check — {projectCfg.Project}",
            FlaggedItems = flagged,
        };
    }
}
