using System.Text.Json;

namespace DashboardApi.Services.Checks;

public class ResolvedPrCheck : ICheckCase
{
    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);
        var header = $"Resolved PR Check — {projectCfg.Project}";

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
            "AND [System.WorkItemType] IN ('Task', 'Bug', 'Defect') " +
            "AND [System.State] = 'Resolved'";

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

        var flagged = new List<FlaggedItem>();
        foreach (var item in rawItems)
        {
            var itemId = item.GetProperty("id").GetInt32();
            var title = Helpers.GetField(item, "System.Title").Trim();
            var wit = Helpers.GetField(item, "System.WorkItemType").Trim();
            var assigned = Helpers.GetField(item, "System.AssignedTo");

            var prDetails = itemPrMap.GetValueOrDefault(itemId, [])
                .Where(pid => prCache.ContainsKey(pid))
                .Select(pid => prCache[pid]).ToList();

            var tags = Helpers.GetTags(item);
            var expectedReleases = Helpers.ReleasesFromTags(tags);

            var requiredReleases = new HashSet<string>(expectedReleases);
            if (sprintRelease is not null) requiredReleases.Add(sprintRelease);
            if (requiredReleases.Count == 0) continue;

            // Skip if there are still active (open) PRs — work is not fully done yet
            var hasOpenPr = prDetails.Any(pr =>
            {
                var status = pr.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                return status.Equals("active", StringComparison.OrdinalIgnoreCase);
            });
            if (hasOpenPr) continue;

            var allCovered = true;
            foreach (var release in requiredReleases)
            {
                var hasCompletedPr = prDetails.Any(pr =>
                {
                    var targetRef = pr.TryGetProperty("targetRefName", out var t) ? t.GetString() ?? "" : "";
                    var prTitle = pr.TryGetProperty("title", out var tt) ? tt.GetString() ?? "" : "";
                    var status = pr.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                    return (targetRef.Contains(release) || prTitle.Contains(release))
                           && status.Equals("completed", StringComparison.OrdinalIgnoreCase);
                });
                if (!hasCompletedPr) { allCovered = false; break; }
            }

            if (allCovered)
            {
                flagged.Add(new FlaggedItem
                {
                    Id = itemId,
                    Title = title,
                    Url = Helpers.WorkItemUrl(projectCfg.Organization, projectCfg.Project, itemId),
                    WorkItemType = $"{wit} [all PRs completed — should be Done]",
                    Project = projectCfg.Project,
                    AssignedTo = assigned,
                });
            }
        }

        return new CaseResult { Header = header, FlaggedItems = flagged };
    }
}
