namespace DashboardApi.Services.Checks;

public class MissingEstimateCheck : ICheckCase
{
    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat, config.EstimateMode));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat, string estimateMode)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);
        var header = $"Missing Estimates — {projectCfg.Project}";

        var currentSprint = await FindCurrentSprintAsync(client, projectCfg);
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
            "AND [System.WorkItemType] IN ('Task','Bug') " +
            "AND [System.State] NOT IN ('Done','Removed','Closed') " +
            "AND (" +
            (estimateMode switch
            {
                "original_estimate" => "  [Microsoft.VSTS.Scheduling.OriginalEstimate] = ''",
                "remaining_work"    => "  [Microsoft.VSTS.Scheduling.RemainingWork] = ''",
                _                   => "  [Microsoft.VSTS.Scheduling.OriginalEstimate] = ''" +
                                       "  OR [Microsoft.VSTS.Scheduling.RemainingWork] = ''",
            }) +
            ")";

        var ids = await client.RunWiqlAsync(wiql);
        if (ids.Count == 0) return new CaseResult { Header = header };

        var rawItems = await client.GetWorkItemsAsync(ids);
        var parentTitles = await Helpers.ResolveParentTitlesAsync(client, rawItems);

        var flagged = new List<FlaggedItem>();
        foreach (var item in rawItems)
        {
            var itemId = item.GetProperty("id").GetInt32();
            var title = Helpers.GetField(item, "System.Title").Trim();
            var wit = Helpers.GetField(item, "System.WorkItemType");
            var original = Helpers.GetNumericField(item, "Microsoft.VSTS.Scheduling.OriginalEstimate");
            var remaining = Helpers.GetNumericField(item, "Microsoft.VSTS.Scheduling.RemainingWork");
            var completed = Helpers.GetNumericField(item, "Microsoft.VSTS.Scheduling.CompletedWork");

            var missing = new List<string>();
            if (estimateMode is "both" or "original_estimate")
            {
                if (!original.HasValue || original.Value == 0) missing.Add("Original Estimate");
            }
            if (estimateMode is "both" or "remaining_work")
            {
                if (!remaining.HasValue || remaining.Value == 0)
                {
                    if (!(original.HasValue && original.Value != 0 && completed.HasValue && completed.Value != 0))
                        missing.Add("Remaining Work");
                }
            }

            if (missing.Count == 0) continue;

            var ptitle = Helpers.GetParentTitle(item, parentTitles).ToLowerInvariant();
            if (projectCfg.IgnoreParentTitleContains.Any(p => ptitle.Contains(p.ToLowerInvariant())))
                continue;

            var assigned = Helpers.GetField(item, "System.AssignedTo");
            var assignedEmail = Helpers.GetIdentityEmail(item, "System.AssignedTo");

            flagged.Add(new FlaggedItem
            {
                Id = itemId,
                Title = title,
                Url = Helpers.WorkItemUrl(projectCfg.Organization, projectCfg.Project, itemId),
                WorkItemType = $"{wit} [missing {string.Join(", ", missing)}]",
                Project = projectCfg.Project,
                AssignedTo = assigned,
                AssignedToEmail = assignedEmail,
            });
        }

        return new CaseResult { Header = header, FlaggedItems = flagged };
    }

    internal static async Task<Dictionary<string, object?>?> FindCurrentSprintAsync(AzureDevOpsClient client, DevOpsProjectConfig projectCfg)
    {
        var now = DateTimeOffset.UtcNow;
        var iterations = await client.GetIterationsAsync();
        var areaPrefix = !string.IsNullOrEmpty(projectCfg.AreaPath)
            ? projectCfg.AreaPath.TrimEnd('\\') + "\\" : "";

        foreach (var it in iterations)
        {
            var path = it["path"]?.ToString() ?? "";
            if (areaPrefix.Length > 0 && !path.StartsWith(areaPrefix)) continue;
            var start = Helpers.ParseDevOpsDate(it["startDate"]?.ToString());
            var finish = Helpers.ParseDevOpsDate(it["finishDate"]?.ToString());
            if (start <= now && now <= finish) return it;
        }
        return null;
    }
}
