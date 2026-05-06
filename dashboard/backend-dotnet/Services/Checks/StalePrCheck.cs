using System.Text.Json;

namespace DashboardApi.Services.Checks;

public class StalePrCheck : ICheckCase
{
    private const int DefaultStaleDays = 14;

    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var staleDays = config.StaleDays > 0 ? config.StaleDays : DefaultStaleDays;
        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat, config.Repository, staleDays));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static DateTimeOffset LatestThreadDate(List<JsonElement> threads)
    {
        var latest = DateTimeOffset.MinValue;
        foreach (var thread in threads)
        {
            if (!thread.TryGetProperty("comments", out var comments)) continue;
            foreach (var comment in comments.EnumerateArray())
            {
                if (comment.TryGetProperty("publishedDate", out var pub))
                {
                    var dt = Helpers.ParseDevOpsDate(pub.GetString());
                    if (dt > latest) latest = dt;
                }
            }
        }
        return latest;
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat,
        string repository, int staleDays)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);
        var header = $"Stale PR Check — {projectCfg.Project}";

        var prs = await client.GetActivePullRequestsAsync(repository);
        if (prs.Count == 0) return new CaseResult { Header = header };

        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-staleDays);

        var potentiallyStale = new List<(JsonElement Pr, DateTimeOffset LastPush)>();
        foreach (var pr in prs)
        {
            // Skip draft PRs — stale counter starts after leaving draft
            if (pr.TryGetProperty("isDraft", out var draft) && draft.GetBoolean())
                continue;

            var creation = Helpers.ParseDevOpsDate(pr.TryGetProperty("creationDate", out var cd) ? cd.GetString() : "");
            var lastPush = creation;

            if (pr.TryGetProperty("lastMergeSourceCommit", out var mc) &&
                mc.TryGetProperty("committer", out var committer) &&
                committer.TryGetProperty("date", out var d))
            {
                var pushDate = Helpers.ParseDevOpsDate(d.GetString());
                if (pushDate > lastPush) lastPush = pushDate;
            }

            if (lastPush >= cutoff) continue;
            potentiallyStale.Add((pr, lastPush));
        }

        if (potentiallyStale.Count == 0) return new CaseResult { Header = header };

        var threadsMap = await client.GetPullRequestThreadsBatchAsync(
            potentiallyStale.Select(x => x.Pr).ToList());

        var flagged = new List<FlaggedItem>();
        foreach (var (pr, lastPush) in potentiallyStale)
        {
            var prId = pr.GetProperty("pullRequestId").GetInt32();
            var threads = threadsMap.GetValueOrDefault(prId, []);
            var lastThread = LatestThreadDate(threads);
            var lastActivity = lastPush > lastThread ? lastPush : lastThread;

            if (lastActivity < cutoff)
            {
                var daysStale = (int)(now - lastActivity).TotalDays;
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
                    WorkItemType = $"PR ({repoName}) — {daysStale}d inactive",
                    Project = projectCfg.Project,
                    AssignedTo = createdBy,
                });
            }
        }

        return new CaseResult { Header = header, FlaggedItems = flagged };
    }
}
