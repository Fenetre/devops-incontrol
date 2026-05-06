namespace DashboardApi.Services.Checks;

public class UnassignedCheck : ICheckCase
{
    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);
        var header = $"Unassigned Items — {projectCfg.Project}";

        var currentSprint = await MissingEstimateCheck.FindCurrentSprintAsync(client, projectCfg);
        if (currentSprint is null)
            return new CaseResult { Header = header };

        var sprintPath = currentSprint["path"]?.ToString() ?? "";
        var areaFilter = !string.IsNullOrEmpty(projectCfg.AreaPath)
            ? $"AND [System.AreaPath] UNDER '{Helpers.EscapeWiql(projectCfg.AreaPath)}' " : "";

        var wiql =
            "SELECT [System.Id] FROM WorkItems " +
            $"WHERE [System.TeamProject] = '{Helpers.EscapeWiql(projectCfg.Project)}' " +
            areaFilter +
            $"AND [System.IterationPath] = '{Helpers.EscapeWiql(sprintPath)}' " +
            "AND [System.State] NOT IN ('Removed','Closed','Done','Completed') " +
            "AND [System.AssignedTo] = ''";

        var ids = await client.RunWiqlAsync(wiql);
        if (ids.Count == 0)
            return new CaseResult { Header = header };

        var rawItems = await client.GetWorkItemsAsync(ids);
        var parentTitles = await Helpers.ResolveParentTitlesAsync(client, rawItems);

        var flagged = new List<FlaggedItem>();
        foreach (var item in rawItems)
        {
            var itemId = item.GetProperty("id").GetInt32();
            var title = Helpers.GetField(item, "System.Title").Trim();
            var wit = Helpers.GetField(item, "System.WorkItemType");

            var ptitle = Helpers.GetParentTitle(item, parentTitles).ToLowerInvariant();
            if (projectCfg.IgnoreParentTitleContains.Any(p => ptitle.Contains(p.ToLowerInvariant())))
                continue;

            flagged.Add(new FlaggedItem
            {
                Id = itemId,
                Title = title,
                Url = Helpers.WorkItemUrl(projectCfg.Organization, projectCfg.Project, itemId),
                WorkItemType = $"{wit} [unassigned]",
                Project = projectCfg.Project,
            });
        }

        return new CaseResult { Header = header, FlaggedItems = flagged };
    }
}
