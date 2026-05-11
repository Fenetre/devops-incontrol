using System.Text.Json.Serialization;

namespace DashboardApi.Models;

/// <summary>API request/response DTOs — match Python models exactly (snake_case JSON).</summary>

// --- Settings ---
public record PatInput([property: JsonPropertyName("pat")] string Pat = "");
public record DbCredentialsInput(
    [property: JsonPropertyName("index")] int Index = 0,
    [property: JsonPropertyName("server")] string Server = "",
    [property: JsonPropertyName("port")] int Port = 1433,
    [property: JsonPropertyName("username")] string Username = "",
    [property: JsonPropertyName("password")] string Password = "",
    [property: JsonPropertyName("driver")] string Driver = "sqlserver");
public record ApiKeyInput([property: JsonPropertyName("api_key")] string ApiKey = "");
public record ApiKeyVerifyInput([property: JsonPropertyName("api_key")] string ApiKey = "");
public record ApiKeyVerifyResponse([property: JsonPropertyName("valid")] bool Valid);
public record PatStatusResponse([property: JsonPropertyName("configured")] bool Configured);
public record DbCredentialsStatusResponse([property: JsonPropertyName("servers")] List<DbServerStatusItem> Servers);
public record DbServerStatusItem(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("configured")] bool Configured,
    [property: JsonPropertyName("server")] string Server = "",
    [property: JsonPropertyName("driver")] string Driver = "sqlserver");
public record DbTestInput([property: JsonPropertyName("index")] int Index = 0);
public record ApiKeyStatusResponse(
    [property: JsonPropertyName("configured")] bool Configured,
    [property: JsonPropertyName("allow_unprotected")] bool AllowUnprotected = false,
    [property: JsonPropertyName("authenticated")] bool Authenticated = false);
public record MessageResponse([property: JsonPropertyName("message")] string Message);
public record EmailFromInput([property: JsonPropertyName("email_from")] string EmailFrom = "");
public record EmailFromStatusResponse(
    [property: JsonPropertyName("configured")] bool Configured,
    [property: JsonPropertyName("email_from")] string EmailFrom = "");
public record SetupStatusResponse(
    [property: JsonPropertyName("setup_complete")] bool SetupComplete,
    [property: JsonPropertyName("pat_configured")] bool PatConfigured,
    [property: JsonPropertyName("project_count")] int ProjectCount);
public record SendMailRequest(
    [property: JsonPropertyName("to")] string To = "",
    [property: JsonPropertyName("assigned_to")] string? AssignedTo = null);
public record SendMailResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("message")] string Message = "");

// --- Projects ---
public record CheckTypeInfo(
    [property: JsonPropertyName("type_key")] string TypeKey,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("default_wiql")] string DefaultWiql);

// --- Checks ---
public class FlaggedItemResponse
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("work_item_type")] public string WorkItemType { get; set; } = "";
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("assigned_to")] public string AssignedTo { get; set; } = "";
    [JsonPropertyName("assigned_to_email")] public string AssignedToEmail { get; set; } = "";
    [JsonPropertyName("created_date")] public string? CreatedDate { get; set; }
    [JsonPropertyName("iteration_path")] public string? IterationPath { get; set; }
    [JsonPropertyName("story_points")] public double? StoryPoints { get; set; }
    [JsonPropertyName("state")] public string? State { get; set; }
}

public class CheckResultResponse
{
    [JsonPropertyName("check_type")] public string CheckType { get; set; } = "";
    [JsonPropertyName("check_label")] public string CheckLabel { get; set; } = "";
    [JsonPropertyName("header")] public string Header { get; set; } = "";
    [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
    [JsonPropertyName("project_name")] public string ProjectName { get; set; } = "";
    [JsonPropertyName("flagged_items")] public List<FlaggedItemResponse> FlaggedItems { get; set; } = [];
    [JsonPropertyName("error")] public string Error { get; set; } = "";
}

public class ProjectResultResponse
{
    [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
    [JsonPropertyName("project_name")] public string ProjectName { get; set; } = "";
    [JsonPropertyName("organization")] public string Organization { get; set; } = "";
    [JsonPropertyName("checks")] public List<CheckResultResponse> Checks { get; set; } = [];
    [JsonPropertyName("total_issues")] public int TotalIssues { get; set; }
    [JsonPropertyName("error")] public string Error { get; set; } = "";
}

public class RunResultResponse
{
    [JsonPropertyName("projects")] public List<ProjectResultResponse> Projects { get; set; } = [];
    [JsonPropertyName("total_issues")] public int TotalIssues { get; set; }
    [JsonPropertyName("ran_at")] public string RanAt { get; set; } = "";
}

// --- Tag work items ---
public class TagWorkItemResponse
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("work_item_type")] public string WorkItemType { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("assigned_to")] public string AssignedTo { get; set; } = "";
}

public class TagWorkItemsResponse
{
    [JsonPropertyName("tag")] public string Tag { get; set; } = "";
    [JsonPropertyName("items")] public List<TagWorkItemResponse> Items { get; set; } = [];
}

// --- Tag operations ---
public class TagOperationRequest
{
    [JsonPropertyName("tag")] public string Tag { get; set; } = "";
    [JsonPropertyName("new_tag")] public string? NewTag { get; set; }
    [JsonPropertyName("work_item_id")] public int? WorkItemId { get; set; }
}

public class TagOperationResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("updated")] public int Updated { get; set; }
}

// --- DevOps ---
public record OrgInfo([property: JsonPropertyName("name")] string Name);
public record ProjectInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] string Id);
public record AreaInfo([property: JsonPropertyName("path")] string Path);
public record RepoInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] string Id);
public record DeleteTagRequest(
    [property: JsonPropertyName("organization")] string Organization,
    [property: JsonPropertyName("project")] string Project,
    [property: JsonPropertyName("tag_name")] string TagName);

// --- DB Projects ---
public class AllDbListResponse
{
    [JsonPropertyName("databases")] public List<string> Databases { get; set; } = [];
    [JsonPropertyName("server_index")] public int ServerIndex { get; set; }
    [JsonPropertyName("server_name")] public string ServerName { get; set; } = "";
}

public class AllDbServersResponse
{
    [JsonPropertyName("servers")] public List<AllDbListResponse> Servers { get; set; } = [];
}

public class DbListResponse
{
    [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
    [JsonPropertyName("project_name")] public string ProjectName { get; set; } = "";
    [JsonPropertyName("name_filter")] public string NameFilter { get; set; } = "";
    [JsonPropertyName("databases")] public List<string> Databases { get; set; } = [];
}

public class DbRuleResult
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("ticket_id")] public int? TicketId { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
}

public class DbRulesResponse
{
    [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
    [JsonPropertyName("project_name")] public string ProjectName { get; set; } = "";
    [JsonPropertyName("results")] public List<DbRuleResult> Results { get; set; } = [];
}

// --- PR Monitor ---
public class PrFlag
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("severity")] public string Severity { get; set; } = "warning";
}

public class PrItem
{
    [JsonPropertyName("pr_id")] public int PrId { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("repository")] public string Repository { get; set; } = "";
    [JsonPropertyName("created_by")] public string CreatedBy { get; set; } = "";
    [JsonPropertyName("creation_date")] public string CreationDate { get; set; } = "";
    [JsonPropertyName("is_draft")] public bool IsDraft { get; set; }
    [JsonPropertyName("reviewer_count")] public int ReviewerCount { get; set; }
    [JsonPropertyName("days_inactive")] public int? DaysInactive { get; set; }
    [JsonPropertyName("flags")] public List<PrFlag> Flags { get; set; } = [];
}

public class PrProjectResponse
{
    [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
    [JsonPropertyName("project_name")] public string ProjectName { get; set; } = "";
    [JsonPropertyName("organization")] public string Organization { get; set; } = "";
    [JsonPropertyName("prs")] public List<PrItem> Prs { get; set; } = [];
    [JsonPropertyName("error")] public string Error { get; set; } = "";
}

// --- DEV Assessment ---
public class DevAssessmentPrItem
{
    [JsonPropertyName("pr_id")] public int PrId { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("repository")] public string Repository { get; set; } = "";
    [JsonPropertyName("created_by")] public string CreatedBy { get; set; } = "";
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("organization")] public string Organization { get; set; } = "";
    [JsonPropertyName("creation_date")] public string CreationDate { get; set; } = "";
    [JsonPropertyName("closed_date")] public string ClosedDate { get; set; } = "";
    [JsonPropertyName("hours_to_complete")] public double? HoursToComplete { get; set; }
    [JsonPropertyName("iterations")] public int Iterations { get; set; }
    [JsonPropertyName("files_changed")] public int FilesChanged { get; set; }
    [JsonPropertyName("lines_added")] public int LinesAdded { get; set; }
    [JsonPropertyName("lines_deleted")] public int LinesDeleted { get; set; }
    [JsonPropertyName("linked_work_item_count")] public int LinkedWorkItemCount { get; set; }
    [JsonPropertyName("hours_to_first_review")] public double? HoursToFirstReview { get; set; }
    [JsonPropertyName("hours_author_response")] public double? HoursAuthorResponse { get; set; }
    [JsonPropertyName("hours_approval_to_merge")] public double? HoursApprovalToMerge { get; set; }
    [JsonPropertyName("review_cycles")] public int ReviewCycles { get; set; }
    [JsonPropertyName("unresolved_thread_count")] public int UnresolvedThreadCount { get; set; }
    [JsonPropertyName("last_delta_after_approval")] public bool LastDeltaAfterApproval { get; set; }
    [JsonPropertyName("self_approved")] public bool SelfApproved { get; set; }
    [JsonPropertyName("reviewers")] public List<DevAssessmentReviewer> Reviewers { get; set; } = [];
}

public class DevAssessmentReviewer
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("vote")] public int Vote { get; set; }
}

public class DevAssessmentWorkItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("work_item_type")] public string WorkItemType { get; set; } = "";
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("organization")] public string Organization { get; set; } = "";
    [JsonPropertyName("assigned_to")] public string AssignedTo { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("created_date")] public string CreatedDate { get; set; } = "";
    [JsonPropertyName("closed_date")] public string? ClosedDate { get; set; }
    [JsonPropertyName("cycle_time_hours")] public double? CycleTimeHours { get; set; }
    [JsonPropertyName("active_age_hours")] public double? ActiveAgeHours { get; set; }
    [JsonPropertyName("original_estimate")] public double? OriginalEstimate { get; set; }
    [JsonPropertyName("completed_work")] public double? CompletedWork { get; set; }
    [JsonPropertyName("remaining_work")] public double? RemainingWork { get; set; }
    [JsonPropertyName("reopen_count")] public int ReopenCount { get; set; }
    [JsonPropertyName("state_changes")] public int StateChanges { get; set; }
}

public class DevAssessmentStaleReviewPr
{
    [JsonPropertyName("pr_id")] public int PrId { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("repository")] public string Repository { get; set; } = "";
    [JsonPropertyName("created_by")] public string CreatedBy { get; set; } = "";
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("organization")] public string Organization { get; set; } = "";
    [JsonPropertyName("creation_date")] public string CreationDate { get; set; } = "";
    [JsonPropertyName("business_days_waiting")] public int BusinessDaysWaiting { get; set; }
    [JsonPropertyName("reviewers")] public List<string> Reviewers { get; set; } = [];
}

public class DevAssessmentOpenPr
{
    [JsonPropertyName("pr_id")] public int PrId { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("repository")] public string Repository { get; set; } = "";
    [JsonPropertyName("created_by")] public string CreatedBy { get; set; } = "";
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("organization")] public string Organization { get; set; } = "";
    [JsonPropertyName("unresolved_thread_count")] public int UnresolvedThreadCount { get; set; }
}

public class DevAssessmentResponse
{
    [JsonPropertyName("prs")] public List<DevAssessmentPrItem> Prs { get; set; } = [];
    [JsonPropertyName("work_items")] public List<DevAssessmentWorkItem> WorkItems { get; set; } = [];
    [JsonPropertyName("stale_review_prs")] public List<DevAssessmentStaleReviewPr> StaleReviewPrs { get; set; } = [];
    [JsonPropertyName("open_prs")] public List<DevAssessmentOpenPr> OpenPrs { get; set; } = [];
    [JsonPropertyName("months")] public int Months { get; set; }
}

public class DevAssessmentRunRequest
{
    [JsonPropertyName("months")] public int Months { get; set; } = 3;
    [JsonPropertyName("project_ids")] public List<string>? ProjectIds { get; set; }
    [JsonPropertyName("since")] public string? Since { get; set; }
    [JsonPropertyName("until")] public string? Until { get; set; }
}

// --- Permission Check ---
public class PermissionMatrixResponse
{
    [JsonPropertyName("teams")] public List<PermissionTeam> Teams { get; set; } = [];
    [JsonPropertyName("members")] public List<PermissionMember> Members { get; set; } = [];
    [JsonPropertyName("categories")] public List<PermissionCategory> Categories { get; set; } = [];
    [JsonPropertyName("matrix")] public List<PermissionEntry> Matrix { get; set; } = [];
    [JsonPropertyName("fetched_at")] public string FetchedAt { get; set; } = "";
}

public class PermissionTeam
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class PermissionMember
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("unique_name")] public string UniqueName { get; set; } = "";
    [JsonPropertyName("image_url")] public string ImageUrl { get; set; } = "";
    [JsonPropertyName("team_names")] public List<string> TeamNames { get; set; } = [];
}

public class PermissionCategory
{
    [JsonPropertyName("namespace_id")] public string NamespaceId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("permissions")] public List<PermissionAction> Permissions { get; set; } = [];
}

public class PermissionAction
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("bit")] public int Bit { get; set; }
}

public class PermissionEntry
{
    [JsonPropertyName("member_id")] public string MemberId { get; set; } = "";
    [JsonPropertyName("namespace_id")] public string NamespaceId { get; set; } = "";
    [JsonPropertyName("permission_name")] public string PermissionName { get; set; } = "";
    [JsonPropertyName("effective")] public string Effective { get; set; } = "not_set";
}

// --- Repo Permissions ---
public class RepoPermissionResponse
{
    [JsonPropertyName("repos")] public List<RepoPermissionEntry> Repos { get; set; } = [];
    [JsonPropertyName("fetched_at")] public string FetchedAt { get; set; } = "";
}

public class RepoPermissionEntry
{
    [JsonPropertyName("repo_id")] public string RepoId { get; set; } = "";
    [JsonPropertyName("repo_name")] public string RepoName { get; set; } = "";
    [JsonPropertyName("permissions")] public List<RepoPermAction> Permissions { get; set; } = [];
}

public class RepoPermAction
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("members_allowed")] public List<string> MembersAllowed { get; set; } = [];
    [JsonPropertyName("members_denied")] public List<string> MembersDenied { get; set; } = [];
}

// --- Area Permissions ---
public class AreaPermissionResponse
{
    [JsonPropertyName("areas")] public List<AreaPermissionEntry> Areas { get; set; } = [];
    [JsonPropertyName("fetched_at")] public string FetchedAt { get; set; } = "";
}

public class AreaPermissionEntry
{
    [JsonPropertyName("area_id")] public string AreaId { get; set; } = "";
    [JsonPropertyName("area_path")] public string AreaPath { get; set; } = "";
    [JsonPropertyName("permissions")] public List<RepoPermAction> Permissions { get; set; } = [];
}

// --- Pipeline Runs ---
public class PipelineRunsResponse
{
    [JsonPropertyName("runs")] public List<PipelineRun> Runs { get; set; } = [];
    [JsonPropertyName("fetched_at")] public string FetchedAt { get; set; } = "";
}

public class PipelineRun
{
    [JsonPropertyName("pipeline_name")] public string PipelineName { get; set; } = "";
    [JsonPropertyName("result")] public string Result { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("branch")] public string Branch { get; set; } = "";
    [JsonPropertyName("requested_by")] public string RequestedBy { get; set; } = "";
    [JsonPropertyName("finished_at")] public string FinishedAt { get; set; } = "";
}

// --- Release Runs ---
public class ReleaseRunsResponse
{
    [JsonPropertyName("runs")] public List<ReleaseRun> Runs { get; set; } = [];
    [JsonPropertyName("fetched_at")] public string FetchedAt { get; set; } = "";
}

public class ReleaseRun
{
    [JsonPropertyName("release_name")] public string ReleaseName { get; set; } = "";
    [JsonPropertyName("definition_name")] public string DefinitionName { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("created_by")] public string CreatedBy { get; set; } = "";
    [JsonPropertyName("created_on")] public string CreatedOn { get; set; } = "";
    [JsonPropertyName("environments")] public List<ReleaseEnvStatus> Environments { get; set; } = [];
}

public class ReleaseEnvStatus
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
}

// --- Velocity ---
public class VelocityIterationInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("start_date")] public string? StartDate { get; set; }
    [JsonPropertyName("finish_date")] public string? FinishDate { get; set; }
    [JsonPropertyName("timeframe")] public string Timeframe { get; set; } = "";
}

public class CapacityActivity
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("capacity_per_day")] public double CapacityPerDay { get; set; }
}

public class CapacityDayOff
{
    [JsonPropertyName("start")] public string Start { get; set; } = "";
    [JsonPropertyName("end")] public string End { get; set; } = "";
}

public class CapacityMemberEntry
{
    [JsonPropertyName("member_id")] public string MemberId { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("activities")] public List<CapacityActivity> Activities { get; set; } = [];
    [JsonPropertyName("days_off")] public List<CapacityDayOff> DaysOff { get; set; } = [];
}

public class TeamCapacityResponse
{
    [JsonPropertyName("iteration_id")] public string IterationId { get; set; } = "";
    [JsonPropertyName("iteration_name")] public string IterationName { get; set; } = "";
    [JsonPropertyName("members")] public List<CapacityMemberEntry> Members { get; set; } = [];
    [JsonPropertyName("total_development")] public double TotalDevelopment { get; set; }
    [JsonPropertyName("total_testing")] public double TotalTesting { get; set; }
    [JsonPropertyName("total_unassigned")] public double TotalUnassigned { get; set; }
}

public class SetCapacityRequest
{
    [JsonPropertyName("team")] public string Team { get; set; } = "";
    [JsonPropertyName("iteration_id")] public string IterationId { get; set; } = "";
    [JsonPropertyName("members")] public List<CapacityMemberEntry> Members { get; set; } = [];
}

public class SprintPointItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("story_points")] public double StoryPoints { get; set; }
    [JsonPropertyName("state")] public string State { get; set; } = "";
}

public class SprintPointsResponse
{
    [JsonPropertyName("iteration_id")] public string IterationId { get; set; } = "";
    [JsonPropertyName("iteration_name")] public string IterationName { get; set; } = "";
    [JsonPropertyName("total_story_points")] public double TotalStoryPoints { get; set; }
    [JsonPropertyName("items")] public List<SprintPointItem> Items { get; set; } = [];
}

public class VelocitySprintInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("story_points")] public double StoryPoints { get; set; }
    [JsonPropertyName("initial_scope")] public double InitialScope { get; set; }
    [JsonPropertyName("burned_points")] public double BurnedPoints { get; set; }
    [JsonPropertyName("remaining_points")] public double RemainingPoints { get; set; }
    [JsonPropertyName("capacity_dev")] public double CapacityDev { get; set; }
    [JsonPropertyName("capacity_test")] public double CapacityTest { get; set; }
    [JsonPropertyName("capacity_unassigned")] public double CapacityUnassigned { get; set; }
    [JsonPropertyName("capacity_total")] public double CapacityTotal { get; set; }
    [JsonPropertyName("timeframe")] public string Timeframe { get; set; } = "";
}

public class VelocityCalcResponse
{
    [JsonPropertyName("last_sprint")] public VelocitySprintInfo LastSprint { get; set; } = new();
    [JsonPropertyName("target_sprint")] public VelocitySprintInfo TargetSprint { get; set; } = new();
    [JsonPropertyName("velocity_ratio")] public double VelocityRatio { get; set; }
    [JsonPropertyName("projected_points")] public double ProjectedPoints { get; set; }
}

public class VelocityMetricsProject
{
    [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
    [JsonPropertyName("project_name")] public string ProjectName { get; set; } = "";
    [JsonPropertyName("team_name")] public string TeamName { get; set; } = "";
    [JsonPropertyName("sprints")] public List<VelocitySprintInfo> Sprints { get; set; } = [];
}

public class VelocityMetricsResponse
{
    [JsonPropertyName("projects")] public List<VelocityMetricsProject> Projects { get; set; } = [];
    [JsonPropertyName("fetched_at")] public string FetchedAt { get; set; } = "";
}

// --- Check Permissions (audit + person search) ---
public class PeopleListResponse
{
    [JsonPropertyName("people")] public List<PersonInfo> People { get; set; } = [];
}

public class PersonInfo
{
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("unique_name")] public string UniqueName { get; set; } = "";
    [JsonPropertyName("organization")] public string Organization { get; set; } = "";
    [JsonPropertyName("descriptor")] public string Descriptor { get; set; } = "";
}

public class PersonSearchResponse
{
    [JsonPropertyName("results")] public List<PersonSearchResult> Results { get; set; } = [];
}

public class PersonSearchResult
{
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("unique_name")] public string UniqueName { get; set; } = "";
    [JsonPropertyName("organization")] public string Organization { get; set; } = "";
    [JsonPropertyName("groups")] public List<PersonGroupInfo> Groups { get; set; } = [];
}

public class PersonGroupInfo
{
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
}

public class PermissionAuditResponse
{
    [JsonPropertyName("projects")] public List<ProjectAuditResult> Projects { get; set; } = [];
    [JsonPropertyName("fetched_at")] public string FetchedAt { get; set; } = "";
}

public class ProjectAuditResult
{
    [JsonPropertyName("organization")] public string Organization { get; set; } = "";
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
    [JsonPropertyName("checks")] public ProjectAuditChecks Checks { get; set; } = new();
}

public class ProjectAuditChecks
{
    [JsonPropertyName("missing_groups")] public List<string> MissingGroups { get; set; } = [];
    [JsonPropertyName("available_groups")] public List<string> AvailableGroups { get; set; } = [];
    [JsonPropertyName("teams_in_groups")] public List<TeamInGroupInfo> TeamsInGroups { get; set; } = [];
    [JsonPropertyName("repos_missing_groups")] public List<RepoMissingGroupsInfo> ReposMissingGroups { get; set; } = [];
    [JsonPropertyName("wiki_missing_groups")] public List<string> WikiMissingGroups { get; set; } = [];
    [JsonPropertyName("wiki_unwanted_groups")] public List<string> WikiUnwantedGroups { get; set; } = [];
    [JsonPropertyName("areas_missing_groups")] public List<AreaMissingGroupsInfo> AreasMissingGroups { get; set; } = [];
}

public class AreaMissingGroupsInfo
{
    [JsonPropertyName("area_path")] public string AreaPath { get; set; } = "";
    [JsonPropertyName("missing_groups")] public List<string> MissingGroups { get; set; } = [];
}

public class TeamInGroupInfo
{
    [JsonPropertyName("group")] public string Group { get; set; } = "";
    [JsonPropertyName("team_name")] public string TeamName { get; set; } = "";
}

public class RepoMissingGroupsInfo
{
    [JsonPropertyName("repo_name")] public string RepoName { get; set; } = "";
    [JsonPropertyName("missing_groups")] public List<string> MissingGroups { get; set; } = [];
}

// --- Release Notes ---
public record ReleaseNoteEntry(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("content")] string Content);

// --- Assign Parent ---
public class ParentTypeHierarchyResponse
{
    [JsonPropertyName("hierarchy")] public Dictionary<string, List<string>> Hierarchy { get; set; } = new();
}

public class CandidateParentItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("work_item_type")] public string WorkItemType { get; set; } = "";
    [JsonPropertyName("area_path")] public string AreaPath { get; set; } = "";
    [JsonPropertyName("iteration_path")] public string IterationPath { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
}

public class CandidateParentResponse
{
    [JsonPropertyName("candidates")] public List<CandidateParentItem> Candidates { get; set; } = [];
}

public class AssignParentRequest
{
    [JsonPropertyName("work_item_id")] public int WorkItemId { get; set; }
    [JsonPropertyName("parent_id")] public int ParentId { get; set; }
}

public class AssignParentResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

// --- Audit Denylist ---
public record AuditDenylistInput([property: JsonPropertyName("key")] string Key = "");

// --- Audit Configuration ---
public class AuditConfigResponse
{
    [JsonPropertyName("group_config")] public List<AuditGroupConnection> GroupConfig { get; set; } = [];
    [JsonPropertyName("rules")] public List<AuditRule> Rules { get; set; } = [];
}

public class AuditConfigInput
{
    [JsonPropertyName("group_config")] public List<AuditGroupConnection> GroupConfig { get; set; } = [];
    [JsonPropertyName("rules")] public List<AuditRule> Rules { get; set; } = [];
}
