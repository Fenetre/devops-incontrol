using System.Text.RegularExpressions;
using DashboardApi.Models;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/projects")]
public partial class ProjectsController(ConfigStore configStore) : ControllerBase
{
    [GeneratedRegex(@"^[a-f0-9]{1,32}$")]
    private static partial Regex ProjectIdRegex();

    private static readonly Dictionary<string, (string Label, string Description)> CheckTypeMeta = new()
    {
        ["orphan_check"] = ("Backlog Orphans", "Work items without a parent or whose parent is done/closed."),
        ["stale_sprint_check"] = ("Stale Sprint Items", "Items still active in a sprint whose end date has passed."),
        ["missing_estimate_check"] = ("Missing Estimates", "Tasks/bugs in the current sprint without Original Estimate or Remaining Work."),
        ["unassigned_check"] = ("Unassigned Items", "Items in the current sprint with no assignee."),
        ["release_pr_check"] = ("Release PR Issues", "Done bugs in the current sprint missing required release pull requests."),
        ["resolved_pr_check"] = ("Resolved PR Ready", "Resolved tasks/bugs with all required PRs completed — ready to move to Done."),
        ["tag_overview_check"] = ("Tag Overview", "Lists all tags in use across the project and how many times each is used."),
        ["pr_approval_check"] = ("PR Approval Ready", "Active PRs that have all required approvals but are not yet completed."),
        ["stale_pr_check"] = ("Stale PRs", "Active PRs with no activity (pushes or comments) in a configurable number of days."),
        ["unreviewed_pr_check"] = ("Unreviewed PRs", "Active PRs that have no reviewers assigned."),
    };

    public static string GetCheckLabel(string checkType)
        => CheckTypeMeta.TryGetValue(checkType, out var m) ? m.Label : checkType;

    [HttpGet]
    public List<ProjectConfig> ListProjects() => configStore.ListProjects();

    [HttpPost]
    [ProducesResponseType(201)]
    public IActionResult AddProject([FromBody] ProjectConfig project)
    {
        foreach (var check in project.Checks) check.Validate();
        var added = configStore.AddProject(project);
        return StatusCode(201, added);
    }

    [HttpPut("{projectId}")]
    public IActionResult UpdateProject(string projectId, [FromBody] ProjectConfig project)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });
        foreach (var check in project.Checks) check.Validate();
        var result = configStore.UpdateProject(projectId, project);
        if (result is null) return NotFound(new { detail = "Project not found" });
        return Ok(result);
    }

    [HttpDelete("{projectId}")]
    public IActionResult DeleteProject(string projectId)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });
        if (!configStore.DeleteProject(projectId))
            return NotFound(new { detail = "Project not found" });
        return Ok(new MessageResponse("Deleted"));
    }

    [HttpGet("checks/types")]
    public List<CheckTypeInfo> ListCheckTypes()
    {
        return CheckTypeMeta.Select(kv => new CheckTypeInfo(
            TypeKey: kv.Key,
            Label: kv.Value.Label,
            Description: kv.Value.Description,
            DefaultWiql: ConfigStore.DefaultWiql.GetValueOrDefault(kv.Key, "")
        )).ToList();
    }

}
