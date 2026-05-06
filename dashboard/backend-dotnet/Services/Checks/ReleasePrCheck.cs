using System.Text.Json;

namespace DashboardApi.Services.Checks;

public class ReleasePrCheck : ICheckCase
{
    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat, config.Repository));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat, string repository)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);
        var header = $"Release PR Check — {projectCfg.Project}";

        var currentSprint = await MissingEstimateCheck.FindCurrentSprintAsync(client, projectCfg);
        if (currentSprint is null)
            return new CaseResult { Header = header };

        var sprintPath = currentSprint["path"]?.ToString() ?? "";
        var sprintRelease = Helpers.ExtractRelease(sprintPath);

        var areaFilter = !string.IsNullOrEmpty(projectCfg.AreaPath)
            ? $"AND [System.AreaPath] UNDER '{Helpers.EscapeWiql(projectCfg.AreaPath)}' " : "";

        var wiql =
            "SELECT [System.Id] FROM WorkItems " +
            $"WHERE [System.TeamProject] = '{Helpers.EscapeWiql(projectCfg.Project)}' " +
            areaFilter +
            $"AND [System.IterationPath] = '{Helpers.EscapeWiql(sprintPath)}' " +
            "AND [System.WorkItemType] IN ('Bug', 'Defect') " +
            "AND [System.State] = 'Done'";

        var ids = await client.RunWiqlAsync(wiql);
        if (ids.Count == 0) return new CaseResult { Header = header };

        var rawItems = await client.GetWorkItemsAsync(ids);

        // Collect all PR IDs
        var allPrIds = new HashSet<int>();
        var itemPrMap = new Dictionary<int, List<int>>();
        foreach (var item in rawItems)
        {
            var itemId = item.GetProperty("id").GetInt32();
            var prIds = Helpers.ExtractPrIds(item);
            itemPrMap[itemId] = prIds;
            foreach (var pid in prIds) allPrIds.Add(pid);
        }

        var prCache = allPrIds.Count > 0
            ? await client.GetPullRequestsBatchAsync(allPrIds.ToList())
            : new Dictionary<int, JsonElement>();

        var repoFilter = (repository ?? "").Trim().ToLowerInvariant();
        var violationItems = new List<(JsonElement Item, string Reason)>();

        foreach (var item in rawItems)
        {
            var itemId = item.GetProperty("id").GetInt32();
            var prDetails = itemPrMap.GetValueOrDefault(itemId, [])
                .Where(pid => prCache.ContainsKey(pid))
                .Select(pid => prCache[pid]).ToList();

            if (!string.IsNullOrEmpty(repoFilter) && prDetails.Count > 0)
            {
                var hasPrInRepo = prDetails.Any(pr =>
                    pr.TryGetProperty("repository", out var repo) &&
                    repo.TryGetProperty("name", out var n) &&
                    (n.GetString() ?? "").ToLowerInvariant() == repoFilter);
                if (!hasPrInRepo) continue;
            }

            var prTargetReleases = new HashSet<string>();
            foreach (var pr in prDetails)
            {
                var targetRef = pr.TryGetProperty("targetRefName", out var t) ? t.GetString() ?? "" : "";
                var rel = Helpers.ExtractRelease(targetRef);
                if (rel is not null) prTargetReleases.Add(rel);
            }

            var tags = Helpers.GetTags(item);
            var expectedReleases = Helpers.ReleasesFromTags(tags);

            var violations = new List<string>();
            foreach (var release in expectedReleases.OrderBy(r => r))
            {
                if (!prTargetReleases.Contains(release))
                    violations.Add($"missing PR for release {release} (tag)");
            }

            if (sprintRelease is not null)
            {
                var hasSprintPr = prDetails.Any(pr =>
                {
                    var title = pr.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    return title.Contains(sprintRelease);
                });
                if (!hasSprintPr)
                    violations.Add($"missing PR for current sprint release {sprintRelease}");
            }

            if (violations.Count > 0)
                violationItems.Add((item, string.Join("; ", violations)));
        }

        // Batch fetch comments
        var violationIds = violationItems.Select(v => v.Item.GetProperty("id").GetInt32()).ToList();
        var commentsMap = violationIds.Count > 0 ? await client.GetCommentsBatchAsync(violationIds) : [];

        var flagged = new List<FlaggedItem>();
        foreach (var (item, reason) in violationItems)
        {
            var itemId = item.GetProperty("id").GetInt32();
            var comments = commentsMap.GetValueOrDefault(itemId, []);
            if (comments.Any(c => c.ToLowerInvariant().Contains("no pr required"))) continue;

            var title = Helpers.GetField(item, "System.Title").Trim();
            flagged.Add(new FlaggedItem
            {
                Id = itemId,
                Title = title,
                Url = Helpers.WorkItemUrl(projectCfg.Organization, projectCfg.Project, itemId),
                WorkItemType = $"Bug [{reason}]",
                Project = projectCfg.Project,
            });
        }

        return new CaseResult { Header = header, FlaggedItems = flagged };
    }
}
