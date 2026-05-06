namespace DashboardApi.Services.Checks;

/// <summary>Maps check_type strings to check implementations.</summary>
public static class CheckRegistry
{
    private static readonly Dictionary<string, Func<ICheckCase>> Registry = new()
    {
        ["orphan_check"] = () => new OrphanCheck(),
        ["stale_sprint_check"] = () => new StaleSprintCheck(),
        ["missing_estimate_check"] = () => new MissingEstimateCheck(),
        ["unassigned_check"] = () => new UnassignedCheck(),
        ["release_pr_check"] = () => new ReleasePrCheck(),
        ["resolved_pr_check"] = () => new ResolvedPrCheck(),
        ["tag_overview_check"] = () => new TagOverviewCheck(),
        ["pr_approval_check"] = () => new PrApprovalCheck(),
        ["stale_pr_check"] = () => new StalePrCheck(),
        ["unreviewed_pr_check"] = () => new UnreviewedPrCheck(),
    };

    public static ICheckCase? GetCase(string checkType)
        => Registry.TryGetValue(checkType, out var factory) ? factory() : null;

    public static IReadOnlyDictionary<string, Func<ICheckCase>> All => Registry;
}
