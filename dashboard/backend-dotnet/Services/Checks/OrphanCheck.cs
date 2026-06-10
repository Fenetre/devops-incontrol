using System.Text.Json;

namespace DashboardApi.Services.Checks;

public class OrphanCheck : ICheckCase
{
    private static readonly HashSet<string> DefaultExcludeTypes = ["Epic"];
    private static readonly HashSet<string> DoneStates = ["removed", "done", "completed", "closed"];

    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var excludeTypes = config.ExcludeTypes.Count > 0
            ? new HashSet<string>(config.ExcludeTypes)
            : DefaultExcludeTypes;

        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat, excludeTypes));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat, HashSet<string> excludeTypes)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);

        var wiql = projectCfg.Wiql;

        // When no custom WIQL or ParentTypeMappings, load types from the process
        // configuration so all configured work item types (including custom ones
        // like Request) are checked automatically.
        if (projectCfg.LoadTypesFromProcess)
        {
            try
            {
                var hierarchy = await client.GetProcessConfigurationAsync();
                if (hierarchy.Count > 0)
                {
                    var types = string.Join(",", hierarchy.Keys.Select(t => $"'{t}'"));
                    wiql =
                        "SELECT [System.Id]\n" +
                        "FROM WorkItems\n" +
                        "WHERE [System.TeamProject] = @project\n" +
                        $"  AND [System.WorkItemType] IN ({types})\n" +
                        "  AND [System.State] NOT IN ('Closed','Removed','Done','Completed')";
                }
            }
            catch
            {
                // Fall back to the default WIQL if process config cannot be loaded
            }
        }

        if (!string.IsNullOrEmpty(projectCfg.AreaPath) && !wiql.Contains("System.AreaPath"))
        {
            var areaClause = Helpers.BuildAreaFilter(projectCfg.AreaPath, projectCfg.IncludeChildAreas).TrimEnd();
            wiql = wiql.TrimEnd().TrimEnd(';') + $"\n  {areaClause}";
        }

        var ids = await client.RunWiqlAsync(wiql);
        var rawItems = await client.GetWorkItemsAsync(ids);

        var stateById = new Dictionary<int, string>();
        foreach (var item in rawItems)
        {
            var id = item.GetProperty("id").GetInt32();
            stateById[id] = Helpers.GetField(item, "System.State").Trim().ToLowerInvariant();
        }

        var missingParentIds = new HashSet<int>();
        foreach (var item in rawItems)
        {
            var pid = Helpers.GetParentId(item);
            if (pid.HasValue && !stateById.ContainsKey(pid.Value))
                missingParentIds.Add(pid.Value);
        }

        if (missingParentIds.Count > 0)
        {
            var parents = await client.GetWorkItemsOrgAsync(missingParentIds.ToList(), fields: ["System.State"]);
            foreach (var pi in parents)
            {
                var id = pi.GetProperty("id").GetInt32();
                stateById[id] = Helpers.GetField(pi, "System.State").Trim().ToLowerInvariant();
            }
        }

        var flagged = new List<FlaggedItem>();
        foreach (var item in rawItems)
        {
            var wit = Helpers.GetField(item, "System.WorkItemType");
            if (excludeTypes.Contains(wit)) continue;

            var itemState = Helpers.GetField(item, "System.State").Trim().ToLowerInvariant();
            if (DoneStates.Contains(itemState)) continue;

            var title = Helpers.GetField(item, "System.Title").Trim();
            if (Helpers.IsAllowlisted(title, projectCfg.IgnoreTitleContains)) continue;

            var parentId = Helpers.GetParentId(item);
            var isOrphan = !parentId.HasValue;
            var hasDeadParent = parentId.HasValue &&
                stateById.TryGetValue(parentId.Value, out var pState) &&
                DoneStates.Contains(pState);

            if (isOrphan || hasDeadParent)
            {
                var itemId = item.GetProperty("id").GetInt32();
                var reason = hasDeadParent ? "parent done" : "no parent";
                flagged.Add(new FlaggedItem
                {
                    Id = itemId,
                    Title = title,
                    Url = Helpers.WorkItemUrl(projectCfg.Organization, projectCfg.Project, itemId),
                    WorkItemType = $"{wit} [{reason}]",
                    Project = projectCfg.Project,
                });
            }
        }

        return new CaseResult
        {
            Header = $"Backlog Orphans — {projectCfg.Project}",
            FlaggedItems = flagged,
        };
    }
}
