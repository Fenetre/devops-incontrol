using System.Text.RegularExpressions;
using System.Web;

namespace DashboardApi.Services.Checks;

/// <summary>Base types and helpers for check cases — port of Python app/cases/base.py + helpers.py.</summary>
/// 
public class FlaggedItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string WorkItemType { get; set; } = "";
    public string Project { get; set; } = "";
    public string AssignedTo { get; set; } = "";
    public string AssignedToEmail { get; set; } = "";
    public string? CreatedDate { get; set; }
    public string? IterationPath { get; set; }
    public double? StoryPoints { get; set; }
    public string? State { get; set; }
}

public class CaseResult
{
    public string Header { get; set; } = "";
    public List<FlaggedItem> FlaggedItems { get; set; } = [];
    public bool HasFlags => FlaggedItems.Count > 0;
}

/// <summary>Configuration passed to each check — mirrors Python CaseConfig.</summary>
public class CaseConfig
{
    public string CaseType { get; set; } = "";
    public List<DevOpsProjectConfig> Projects { get; set; } = [];
    public List<string> ExcludeTypes { get; set; } = [];
    public string Repository { get; set; } = "";
    public int StaleDays { get; set; } = 14;
    public List<string> IgnoreReviewers { get; set; } = [];
    /// <summary>For missing_estimate_check: "both" (default), "original_estimate", or "remaining_work".</summary>
    public string EstimateMode { get; set; } = "both";
}

public class DevOpsProjectConfig
{
    public string Organization { get; set; } = "";
    public string Project { get; set; } = "";
    public string ApiVersion { get; set; } = "7.1";
    public string Wiql { get; set; } = "";
    public string AreaPath { get; set; } = "";
    public List<string> IgnoreTitleContains { get; set; } = [];
    public List<string> IgnoreParentTitleContains { get; set; } = [];
}

public interface ICheckCase
{
    Task<List<CaseResult>> RunAsync(CaseConfig config, string pat);
}

public static partial class Helpers
{
    private const string IlseName = "ilse";

    [GeneratedRegex(@"^[\w .\-–—\\\/,;:()&+#@!êéèëàáâäöüïîôçñ]{1,512}$", RegexOptions.None)]
    private static partial Regex SafeDevOpsNameRegex();

    public static DateTimeOffset ParseDevOpsDate(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return DateTimeOffset.MinValue;
        var cleaned = raw.Replace("Z", "+00:00");
        return DateTimeOffset.Parse(cleaned, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public static string PrUrl(string organization, string project, string repoName, int prId)
    {
        var org = Uri.EscapeDataString(organization);
        var proj = Uri.EscapeDataString(project);
        return $"https://dev.azure.com/{org}/{proj}/_git/{repoName}/pullrequest/{prId}";
    }

    public static string WorkItemUrl(string organization, string project, int itemId)
    {
        var org = Uri.EscapeDataString(organization);
        var proj = Uri.EscapeDataString(project);
        return $"https://dev.azure.com/{org}/{proj}/_workitems/edit/{itemId}";
    }

    public static bool IsApproved(int vote) => vote >= 5;

    public static bool IsIlse(System.Text.Json.JsonElement reviewer)
    {
        var name = "";
        if (reviewer.TryGetProperty("displayName", out var dn))
            name = (dn.GetString() ?? "").ToLowerInvariant();
        return name == IlseName || name.StartsWith(IlseName + " ", StringComparison.Ordinal);
    }

    public static string EscapeWiql(string value)
    {
        if (!SafeDevOpsNameRegex().IsMatch(value))
            throw new ArgumentException($"Invalid WIQL value: {value}");
        return value.Replace("'", "''");
    }

    public static int? GetParentId(System.Text.Json.JsonElement item)
    {
        if (!item.TryGetProperty("relations", out var rels)) return null;
        foreach (var rel in rels.EnumerateArray())
        {
            if (rel.TryGetProperty("rel", out var relType) &&
                relType.GetString() == "System.LinkTypes.Hierarchy-Reverse" &&
                rel.TryGetProperty("url", out var urlProp))
            {
                var url = urlProp.GetString() ?? "";
                var lastSlash = url.TrimEnd('/').LastIndexOf('/');
                if (lastSlash >= 0 && int.TryParse(url[(lastSlash + 1)..].TrimEnd('/'), out var parentId))
                    return parentId;
            }
        }
        return null;
    }

    public static string GetField(System.Text.Json.JsonElement item, string fieldName)
    {
        if (item.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty(fieldName, out var val))
        {
            if (val.ValueKind == System.Text.Json.JsonValueKind.String)
                return val.GetString() ?? "";
            if (val.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (val.TryGetProperty("displayName", out var dn))
                    return dn.GetString() ?? "";
            }
            if (val.ValueKind == System.Text.Json.JsonValueKind.Number)
                return val.ToString();
        }
        return "";
    }

    public static string GetIdentityEmail(System.Text.Json.JsonElement item, string fieldName)
    {
        if (item.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty(fieldName, out var val) &&
            val.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (val.TryGetProperty("uniqueName", out var un))
                return un.GetString() ?? "";
        }
        return "";
    }

    public static double? GetNumericField(System.Text.Json.JsonElement item, string fieldName)
    {
        if (item.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty(fieldName, out var val))
        {
            if (val.ValueKind == System.Text.Json.JsonValueKind.Number)
                return val.GetDouble();
        }
        return null;
    }

    public static bool IsAllowlisted(string title, List<string> patterns)
    {
        var lower = title.ToLowerInvariant();
        return patterns.Any(p => lower.Contains(p.ToLowerInvariant()));
    }

    public static List<int> ExtractPrIds(System.Text.Json.JsonElement item)
    {
        var prIds = new List<int>();
        if (!item.TryGetProperty("relations", out var rels)) return prIds;
        foreach (var rel in rels.EnumerateArray())
        {
            if (rel.TryGetProperty("rel", out var relType) && relType.GetString() == "ArtifactLink" &&
                rel.TryGetProperty("attributes", out var attrs) &&
                attrs.TryGetProperty("name", out var name) && name.GetString() == "Pull Request" &&
                rel.TryGetProperty("url", out var urlProp))
            {
                var url = HttpUtility.UrlDecode(urlProp.GetString() ?? "");
                var lastSlash = url.TrimEnd('/').LastIndexOf('/');
                if (lastSlash >= 0 && int.TryParse(url[(lastSlash + 1)..].TrimEnd('/'), out var prId))
                    prIds.Add(prId);
            }
        }
        return prIds;
    }

    public static string? ExtractRelease(string text)
    {
        var m = Regex.Match(text, @"(\d+\.\d+(?:\.\d+)?)");
        return m.Success ? m.Groups[1].Value : null;
    }

    public static List<string> GetTags(System.Text.Json.JsonElement item)
    {
        var tagsStr = GetField(item, "System.Tags");
        if (string.IsNullOrEmpty(tagsStr)) return [];
        return tagsStr.Split(';').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
    }

    public static HashSet<string> ReleasesFromTags(List<string> tags)
    {
        var releases = new HashSet<string>();
        foreach (var tag in tags)
        {
            var m = Regex.Match(tag, @"(\d+\.\d+(?:\.\d+)?)");
            if (m.Success) releases.Add(m.Groups[1].Value);
        }
        return releases;
    }

    public static string GetParentTitle(System.Text.Json.JsonElement item, Dictionary<int, string> parentTitles)
    {
        if (!item.TryGetProperty("relations", out var rels)) return "";
        foreach (var rel in rels.EnumerateArray())
        {
            if (rel.TryGetProperty("rel", out var relType) &&
                relType.GetString() == "System.LinkTypes.Hierarchy-Reverse" &&
                rel.TryGetProperty("url", out var urlProp))
            {
                var url = urlProp.GetString() ?? "";
                var lastSlash = url.TrimEnd('/').LastIndexOf('/');
                if (lastSlash >= 0 && int.TryParse(url[(lastSlash + 1)..].TrimEnd('/'), out var parentId))
                    return parentTitles.GetValueOrDefault(parentId, "");
            }
        }
        return "";
    }

    public static async Task<Dictionary<int, string>> ResolveParentTitlesAsync(AzureDevOpsClient client, List<System.Text.Json.JsonElement> items)
    {
        var parentIds = new HashSet<int>();
        foreach (var item in items)
        {
            var pid = GetParentId(item);
            if (pid.HasValue) parentIds.Add(pid.Value);
        }

        var titles = new Dictionary<int, string>();
        if (parentIds.Count == 0) return titles;

        var parents = await client.GetWorkItemsAsync(parentIds.ToList(), fields: ["System.Title"], expand: "None");
        foreach (var p in parents)
        {
            if (p.TryGetProperty("id", out var id))
                titles[id.GetInt32()] = GetField(p, "System.Title").Trim();
        }
        return titles;
    }
}
