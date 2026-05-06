using System.Text.RegularExpressions;
using System.Web;

namespace DashboardApi.Services.Checks;

public partial class TagOverviewCheck : ICheckCase
{
    [GeneratedRegex(@"^\d+(\.\d+){2,}$")]
    private static partial Regex VersionTagRegex();

    public async Task<List<CaseResult>> RunAsync(CaseConfig config, string pat)
    {
        var tasks = config.Projects.Select(p => CheckProjectAsync(p, pat));
        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<CaseResult> CheckProjectAsync(DevOpsProjectConfig projectCfg, string pat)
    {
        var client = new AzureDevOpsClient(projectCfg.Organization, projectCfg.Project, pat, projectCfg.ApiVersion);
        var header = $"Tag Overview — {projectCfg.Project}";

        var areaFilter = !string.IsNullOrEmpty(projectCfg.AreaPath)
            ? $" AND [System.AreaPath] UNDER '{Helpers.EscapeWiql(projectCfg.AreaPath)}'" : "";

        var wiql =
            "SELECT [System.Id] FROM WorkItems " +
            $"WHERE [System.TeamProject] = '{Helpers.EscapeWiql(projectCfg.Project)}'" +
            areaFilter +
            " AND [System.State] NOT IN ('Removed','Closed')";

        List<System.Text.Json.JsonElement> rawItems;
        try
        {
            var ids = await client.RunWiqlAsync(wiql);
            rawItems = ids.Count > 0
                ? await client.GetWorkItemsAsync(ids, fields: ["System.Tags"], expand: "None")
                : [];
        }
        catch
        {
            var org = HttpUtility.UrlEncode(projectCfg.Organization);
            var proj = HttpUtility.UrlEncode(projectCfg.Project);
            return new CaseResult
            {
                Header = header,
                FlaggedItems = [new FlaggedItem
                {
                    Id = 0,
                    Title = "⚠ Too many work items (>20 000) — tag overview unavailable",
                    Url = $"https://dev.azure.com/{org}/{proj}/_workitems",
                    WorkItemType = "warning",
                    Project = projectCfg.Project,
                }],
            };
        }

        var counter = new Dictionary<string, int>();
        foreach (var item in rawItems)
        {
            var tagsRaw = Helpers.GetField(item, "System.Tags").Trim();
            if (string.IsNullOrEmpty(tagsRaw)) continue;
            foreach (var tag in tagsRaw.Split(';'))
            {
                var t = tag.Trim();
                if (t.Length > 0 && !VersionTagRegex().IsMatch(t))
                    counter[t] = counter.GetValueOrDefault(t) + 1;
            }
        }

        // Include tags defined in project but not in use
        var allTags = await client.GetTagsAsync();
        foreach (var tag in allTags)
        {
            var t = tag.Trim();
            if (t.Length > 0 && !VersionTagRegex().IsMatch(t) && !counter.ContainsKey(t))
                counter[t] = 0;
        }

        var org2 = HttpUtility.UrlEncode(projectCfg.Organization);
        var proj2 = HttpUtility.UrlEncode(projectCfg.Project);
        var flagged = new List<FlaggedItem>();
        var idx = 0;
        foreach (var (tag, count) in counter.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
        {
            idx++;
            var label = $"{count} item{(count != 1 ? "s" : "")}";
            flagged.Add(new FlaggedItem
            {
                Id = idx,
                Title = tag,
                Url = $"https://dev.azure.com/{org2}/{proj2}/_workitems?_a=query",
                WorkItemType = label,
                Project = projectCfg.Project,
            });
        }

        return new CaseResult { Header = header, FlaggedItems = flagged };
    }
}
