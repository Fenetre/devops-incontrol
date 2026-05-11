namespace DashboardApi.Services.Checks;

public class StaleSprintCheck : ICheckCase
{
    private const int DefaultLookbackDays = 90;

    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat,
        int lookbackDays = DefaultLookbackDays)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);
        var now = DateTimeOffset.UtcNow;
        // Use start-of-today so a sprint whose finishDate is today is still
        // considered active (its items should not be flagged until tomorrow).
        var today = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var cutoff = today.AddDays(-lookbackDays);
        var iterations = await client.GetIterationsAsync();

        var completed = new List<(string Path, DateTimeOffset Finish)>();
        foreach (var it in iterations)
        {
            var finish = Helpers.ParseDevOpsDate(it["finishDate"]?.ToString());
            if (finish >= cutoff && finish < today)
                completed.Add((it["path"]?.ToString() ?? "", finish));
        }

        var header = $"Stale Sprint Items — {projectCfg.Project}";
        if (completed.Count == 0)
            return new CaseResult { Header = header };

        completed.Sort((a, b) => a.Finish.CompareTo(b.Finish));
        var latestSprintPath = completed[^1].Path;
        var completedPaths = completed.Select(c => c.Path).ToList();

        var pathClauses = string.Join(" OR ",
            completedPaths.Select(p => $"[System.IterationPath] = '{Helpers.EscapeWiql(p)}'"));

        var areaFilter = Helpers.BuildAreaFilter(projectCfg.AreaPath, projectCfg.IncludeChildAreas);

        var wiql =
            "SELECT [System.Id] FROM WorkItems " +
            $"WHERE [System.TeamProject] = '{Helpers.EscapeWiql(projectCfg.Project)}' " +
            areaFilter +
            $"AND ({pathClauses}) " +
            "AND [System.State] NOT IN ('Done','Completed','Removed','Closed')";

        var ids = await client.RunWiqlAsync(wiql);
        if (ids.Count == 0)
            return new CaseResult { Header = header };

        var rawItems = await client.GetWorkItemsAsync(ids, fields: [
            "System.Title", "System.WorkItemType", "System.IterationPath", "System.State"
        ], expand: "None");

        var flagged = new List<FlaggedItem>();
        foreach (var item in rawItems)
        {
            var itemId = item.GetProperty("id").GetInt32();
            var title = Helpers.GetField(item, "System.Title").Trim();
            var wit = Helpers.GetField(item, "System.WorkItemType");
            var iteration = Helpers.GetField(item, "System.IterationPath");
            var state = Helpers.GetField(item, "System.State");

            var inLatestSprint = iteration == latestSprintPath;
            if (inLatestSprint && Helpers.IsAllowlisted(title, projectCfg.IgnoreTitleContains))
                continue;

            flagged.Add(new FlaggedItem
            {
                Id = itemId,
                Title = title,
                Url = Helpers.WorkItemUrl(projectCfg.Organization, projectCfg.Project, itemId),
                WorkItemType = $"{wit} [{state} · {iteration}]",
                Project = projectCfg.Project,
            });
        }

        return new CaseResult { Header = header, FlaggedItems = flagged };
    }
}
