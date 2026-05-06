using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DashboardApi.Models;

/// <summary>Configuration persisted in config.json — matches Python Pydantic models exactly.</summary>

public partial class ProjectCheckConfig
{
    [JsonPropertyName("check_type")]
    public string CheckType { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("api_version")]
    public string ApiVersion { get; set; } = "7.1";

    [JsonPropertyName("exclude_types")]
    public List<string> ExcludeTypes { get; set; } = [];

    [JsonPropertyName("custom_wiql")]
    public string CustomWiql { get; set; } = "";

    [JsonPropertyName("repository")]
    public string Repository { get; set; } = "";

    [JsonPropertyName("stale_days")]
    public int StaleDays { get; set; } = 14;

    [JsonPropertyName("ignore_reviewers")]
    public List<string> IgnoreReviewers { get; set; } = [];

    /// <summary>For missing_estimate_check: "both" (default), "original_estimate", or "remaining_work".</summary>
    [JsonPropertyName("estimate_mode")]
    public string EstimateMode { get; set; } = "both";

    [GeneratedRegex(@"\b(DROP|ALTER|DELETE|INSERT|UPDATE|EXEC|EXECUTE|TRUNCATE|CREATE|GRANT|REVOKE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WiqlDenyPattern();

    [GeneratedRegex(@"\[System\.TeamProject\]", RegexOptions.IgnoreCase)]
    private static partial Regex TeamProjectPattern();

    public void Validate()
    {
        if (!string.IsNullOrEmpty(CustomWiql))
        {
            if (WiqlDenyPattern().IsMatch(CustomWiql))
                throw new ArgumentException("Custom WIQL contains forbidden statements.");
            if (TeamProjectPattern().IsMatch(CustomWiql))
                throw new ArgumentException(
                    "Custom WIQL must not reference [System.TeamProject]; the project is set automatically.");
        }
    }
}

public class ProjectConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("organization")]
    public string Organization { get; set; } = "";

    [JsonPropertyName("project")]
    public string Project { get; set; } = "";

    [JsonPropertyName("area_path")]
    public string AreaPath { get; set; } = "";

    [JsonPropertyName("ignore_title_contains")]
    public List<string> IgnoreTitleContains { get; set; } = [];

    [JsonPropertyName("ignore_parent_title_contains")]
    public List<string> IgnoreParentTitleContains { get; set; } = [];

    [JsonPropertyName("checks")]
    public List<ProjectCheckConfig> Checks { get; set; } = [];
}

public class DbProjectConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("name_filter")]
    public string NameFilter { get; set; } = "";

    [JsonPropertyName("db_server_index")]
    public int DbServerIndex { get; set; } = 0;

    [JsonPropertyName("db_allowlist")]
    public List<string> DbAllowlist { get; set; } = [];
}

public class DbServerConfig
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 1433;

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("driver")]
    public string Driver { get; set; } = "sqlserver";  // "sqlserver" or "postgres"
}

public class DashboardConfig
{
    [JsonPropertyName("pat")]
    public string Pat { get; set; } = "";

    // Legacy single-server fields (migrated to db_servers on first load)
    [JsonPropertyName("db_server")]
    public string DbServer { get; set; } = "";

    [JsonPropertyName("db_port")]
    public int DbPort { get; set; } = 1433;

    [JsonPropertyName("db_username")]
    public string DbUsername { get; set; } = "";

    [JsonPropertyName("db_password")]
    public string DbPassword { get; set; } = "";

    [JsonPropertyName("db_servers")]
    public List<DbServerConfig> DbServers { get; set; } = [];

    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("email_from")]
    public string EmailFrom { get; set; } = "";

    [JsonPropertyName("smtp_host")]
    public string SmtpHost { get; set; } = "smtp.fenetre.nl";

    [JsonPropertyName("smtp_port")]
    public int SmtpPort { get; set; } = 587;

    [JsonPropertyName("projects")]
    public List<ProjectConfig> Projects { get; set; } = [];

    [JsonPropertyName("db_projects")]
    public List<DbProjectConfig> DbProjects { get; set; } = [];

    [JsonPropertyName("setup_complete")]
    public bool SetupComplete { get; set; }

    [JsonPropertyName("audit_denylist")]
    public List<string> AuditDenylist { get; set; } = [];

    [JsonPropertyName("audit_group_config")]
    public List<AuditGroupConnection> AuditGroupConfig { get; set; } = [];

    [JsonPropertyName("audit_rules")]
    public List<AuditRule> AuditRules { get; set; } = [];
}

public class AuditGroupConnection
{
    [JsonPropertyName("group_name")]
    public string GroupName { get; set; } = "";

    [JsonPropertyName("area_connected")]
    public bool AreaConnected { get; set; }

    [JsonPropertyName("repo_connected")]
    public bool RepoConnected { get; set; }

    [JsonPropertyName("wiki_connected")]
    public bool WikiConnected { get; set; }
}

public class AuditRule
{
    /// <summary>Rule type: "customer_deny", "team_deny", or "mandatory_group".</summary>
    [JsonPropertyName("rule_type")]
    public string RuleType { get; set; } = "";

    /// <summary>Target group name (used by customer_deny and mandatory_group).</summary>
    [JsonPropertyName("group_name")]
    public string GroupName { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
