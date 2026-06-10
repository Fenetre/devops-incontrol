using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashboardApi.Models;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/db-projects")]
public partial class DbProjectsController(ConfigStore configStore) : ControllerBase
{
    [GeneratedRegex(@"^[a-f0-9]{1,32}$")]
    private static partial Regex ProjectIdRegex();

    [GeneratedRegex(@"(?<!\d)(\d{5,6})(?!\d)")]
    private static partial Regex TicketIdRegex();

    [HttpGet]
    public List<DbProjectConfig> ListDbProjects() => configStore.ListDbProjects();

    [HttpPost]
    [ProducesResponseType(201)]
    public IActionResult AddDbProject([FromBody] DbProjectConfig project)
    {
        var added = configStore.AddDbProject(project);
        return StatusCode(201, added);
    }

    [HttpPut("{projectId}")]
    public IActionResult UpdateDbProject(string projectId, [FromBody] DbProjectConfig project)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });
        var result = configStore.UpdateDbProject(projectId, project);
        if (result is null) return NotFound(new { detail = "DB project not found" });
        return Ok(result);
    }

    [HttpDelete("{projectId}")]
    public IActionResult DeleteDbProject(string projectId)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });
        if (!configStore.DeleteDbProject(projectId))
            return NotFound(new { detail = "DB project not found" });
        return Ok(new MessageResponse("Deleted"));
    }

    [HttpGet("databases/all")]
    public async Task<IActionResult> ListAllDatabases(CancellationToken cancellationToken = default)
    {
        var cfg = configStore.LoadConfig();
        var results = new List<AllDbListResponse>();

        for (int i = 0; i < cfg.DbServers.Count && i < 3; i++)
        {
            var creds = SettingsController.GetDbCredentials(configStore, i);
            if (creds is null) continue;

            try
            {
                var dbs = await DbConnector.ListDatabaseNamesAsync(
                    creds.Value.Server, creds.Value.Port, creds.Value.Username, creds.Value.Password, creds.Value.Driver);
                results.Add(new AllDbListResponse
                {
                    ServerIndex = i,
                    ServerName = creds.Value.Server,
                    Databases = dbs,
                });
            }
            catch
            {
                // Skip unreachable servers
            }
        }

        if (results.Count == 0)
            return BadRequest(new { detail = "No database servers configured or reachable" });

        // Flatten for backward compat (allDatabases)
        var allDbs = results.SelectMany(r => r.Databases).Distinct().OrderBy(d => d).ToList();
        return Ok(new { databases = allDbs, servers = results });
    }

    [HttpGet("{projectId}/databases")]
    public async Task<IActionResult> ListDatabases(string projectId, CancellationToken cancellationToken = default)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });

        var proj = configStore.ListDbProjects().FirstOrDefault(p => p.Id == projectId);
        if (proj is null) return NotFound(new { detail = "DB project not found" });

        var creds = SettingsController.GetDbCredentials(configStore, proj.DbServerIndex);
        if (creds is null)
            return BadRequest(new { detail = $"Database credentials not configured for server #{proj.DbServerIndex + 1}" });

        List<string> allDbs;
        try
        {
            allDbs = await DbConnector.ListDatabaseNamesAsync(
                creds.Value.Server, creds.Value.Port, creds.Value.Username, creds.Value.Password, creds.Value.Driver);
        }
        catch
        {
            return StatusCode(502, new { detail = $"Failed to connect to database server #{proj.DbServerIndex + 1}" });
        }

        var databases = !string.IsNullOrEmpty(proj.NameFilter)
            ? allDbs.Where(db => db.Contains(proj.NameFilter, StringComparison.OrdinalIgnoreCase)).ToList()
            : allDbs;

        return Ok(new DbListResponse
        {
            ProjectId = proj.Id,
            ProjectName = proj.Name,
            NameFilter = proj.NameFilter,
            Databases = databases,
        });
    }

    [HttpPost("{projectId}/allowlist/toggle")]
    public IActionResult ToggleAllowlist(string projectId, [FromBody] DbAllowlistToggleRequest request)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });

        if (string.IsNullOrWhiteSpace(request.DatabaseName))
            return BadRequest(new { detail = "Database name is required." });

        var proj = configStore.ListDbProjects().FirstOrDefault(p => p.Id == projectId);
        if (proj is null) return NotFound(new { detail = "DB project not found" });

        var allowlist = proj.DbAllowlist ?? [];
        var existing = allowlist.FirstOrDefault(
            e => string.Equals(e, request.DatabaseName, StringComparison.OrdinalIgnoreCase));

        bool added;
        if (existing is not null)
        {
            allowlist.Remove(existing);
            added = false;
        }
        else
        {
            allowlist.Add(request.DatabaseName);
            added = true;
        }

        proj.DbAllowlist = allowlist;
        configStore.UpdateDbProject(projectId, proj);
        return Ok(new { added, database_name = request.DatabaseName, allowlist = proj.DbAllowlist });
    }

    [HttpPost("{projectId}/databases/check-rules")]
    public async Task<IActionResult> CheckDatabaseRules(string projectId, CancellationToken cancellationToken = default)
    {
        if (!ProjectIdRegex().IsMatch(projectId))
            return BadRequest(new { detail = "Invalid project ID." });

        try
        {
            return Ok(await CheckDatabaseRulesAsync(projectId));
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    private async Task<DbRulesResponse> CheckDatabaseRulesAsync(string projectId)
    {
        var proj = configStore.ListDbProjects().FirstOrDefault(p => p.Id == projectId);
        if (proj is null) throw new BadHttpRequestException("DB project not found");

        var creds = SettingsController.GetDbCredentials(configStore, proj.DbServerIndex);
        if (creds is null) throw new BadHttpRequestException($"Database credentials not configured for server #{proj.DbServerIndex + 1}");

        List<string> allDbs;
        try
        {
            allDbs = await DbConnector.ListDatabaseNamesAsync(creds.Value.Server, creds.Value.Port,
                creds.Value.Username, creds.Value.Password, creds.Value.Driver);
        }
        catch (Exception ex) { throw new BadHttpRequestException($"Failed to connect to {creds.Value.Driver} server #{proj.DbServerIndex + 1} ({creds.Value.Server}:{creds.Value.Port}): {ex.Message}"); }

        var dbs = !string.IsNullOrEmpty(proj.NameFilter)
            ? allDbs.Where(db => db.Contains(proj.NameFilter, StringComparison.OrdinalIgnoreCase)).ToList()
            : allDbs;

        // Extract 5-or-6-digit ticket numbers
        var ticketRe = TicketIdRegex();
        var dbTickets = new Dictionary<string, int?>();
        var ticketIds = new HashSet<int>();
        foreach (var db in dbs)
        {
            var match = ticketRe.Match(db);
            if (match.Success)
            {
                var tid = int.Parse(match.Groups[1].Value);
                dbTickets[db] = tid;
                ticketIds.Add(tid);
            }
            else
            {
                dbTickets[db] = null;
            }
        }

        // Query Azure DevOps for existing and closed items
        var existingIds = new HashSet<int>();
        var closedIds = new HashSet<int>();
        if (ticketIds.Count > 0)
        {
            var pat = SettingsController.GetPat(configStore);
            if (!string.IsNullOrEmpty(pat))
            {
                var orgs = configStore.LoadConfig().Projects
                    .Select(p => p.Organization)
                    .Where(o => !string.IsNullOrEmpty(o))
                    .Distinct()
                    .ToList();

                var idCsv = string.Join(", ", ticketIds);
                var wiqlAll = $"SELECT [System.Id] FROM WorkItems " +
                              $"WHERE [System.Id] IN ({idCsv})";
                var wiqlClosed = $"SELECT [System.Id] FROM WorkItems " +
                                 $"WHERE [System.Id] IN ({idCsv}) " +
                                 $"AND [System.State] IN ('Closed', 'Done', 'Removed')";

                var http = HttpClientPool.Get(pat, 30);

                var orgTasks = orgs.Select(async org =>
                {
                    try
                    {
                        var url = $"https://dev.azure.com/{org}/_apis/wit/wiql?api-version=7.1";

                        // Run both WIQL queries in parallel per org
                        var contentAll = new StringContent(
                            JsonSerializer.Serialize(new { query = wiqlAll }),
                            Encoding.UTF8, "application/json");
                        var contentClosed = new StringContent(
                            JsonSerializer.Serialize(new { query = wiqlClosed }),
                            Encoding.UTF8, "application/json");

                        var allTask = http.PostAsync(url, contentAll);
                        var closedTask = http.PostAsync(url, contentClosed);
                        await Task.WhenAll(allTask, closedTask);

                        var respAll = await allTask;
                        AzureDevOpsClient.ThrowIfPatScopeError(respAll, url);
                        if (respAll.IsSuccessStatusCode)
                        {
                            var doc = await JsonDocument.ParseAsync(await respAll.Content.ReadAsStreamAsync());
                            if (doc.RootElement.TryGetProperty("workItems", out var wis))
                                foreach (var wi in wis.EnumerateArray())
                                    if (wi.TryGetProperty("id", out var id))
                                        lock (existingIds) { existingIds.Add(id.GetInt32()); }
                        }

                        var respClosed = await closedTask;
                        AzureDevOpsClient.ThrowIfPatScopeError(respClosed, url);
                        if (respClosed.IsSuccessStatusCode)
                        {
                            var doc = await JsonDocument.ParseAsync(await respClosed.Content.ReadAsStreamAsync());
                            if (doc.RootElement.TryGetProperty("workItems", out var wis))
                                foreach (var wi in wis.EnumerateArray())
                                    if (wi.TryGetProperty("id", out var id))
                                        lock (closedIds) { closedIds.Add(id.GetInt32()); }
                        }
                    }
                    catch { /* best-effort — skip unreachable orgs */ }
                });
                await Task.WhenAll(orgTasks);
            }
        }

        // Build results (allowlist overrides to ok)
        var allowlist = new HashSet<string>(proj.DbAllowlist ?? [], StringComparer.OrdinalIgnoreCase);
        var results = dbs.Select(db =>
        {
            var tid = dbTickets[db];
            string status;
            if (allowlist.Contains(db)) status = "allowlisted";
            else if (!tid.HasValue) status = "no_ticket";
            else if (!existingIds.Contains(tid.Value)) status = "no_ticket";
            else if (closedIds.Contains(tid.Value)) status = "closed_ticket";
            else status = "ok";

            return new DbRuleResult { Name = db, TicketId = tid, Status = status };
        }).ToList();

        return new DbRulesResponse
        {
            ProjectId = proj.Id,
            ProjectName = proj.Name,
            Results = results,
        };
    }
}
