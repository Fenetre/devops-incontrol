using System.Text.Json;
using System.Text.Json.Serialization;
using DashboardApi.Models;

namespace DashboardApi.Services;

/// <summary>
/// Config.json persistence with mtime-based caching and thread-safe locking.
/// Matches Python config_store.py behavior exactly.
/// </summary>
public class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = null, // Use JsonPropertyName attributes
    };

    private readonly string _configPath;
    private readonly object _lock = new();
    private DashboardConfig? _cached;
    private DateTime _cachedMtime;
    private FileSystemWatcher? _watcher;

    public ConfigStore(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(
            Path.GetDirectoryName(typeof(ConfigStore).Assembly.Location)!,
            "..", "..", "data", "config.json");

        // Resolve to absolute path relative to the project
        if (!Path.IsPathRooted(_configPath))
            _configPath = Path.GetFullPath(_configPath);

        InitWatcher();
    }

    private void InitWatcher()
    {
        var dir = Path.GetDirectoryName(_configPath);
        var file = Path.GetFileName(_configPath);
        if (dir is null || !Directory.Exists(dir)) return;

        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) =>
        {
            lock (_lock) { _cached = null; }
        };
    }

    // --- Default WIQL templates per check type ---
    public static readonly Dictionary<string, string> DefaultWiql = new()
    {
        ["orphan_check"] =
            "SELECT [System.Id]\n" +
            "FROM WorkItems\n" +
            "WHERE [System.TeamProject] = @project\n" +
            "  AND [System.WorkItemType] IN ('Epic','Feature','User Story','Bug','Defect','Task')\n" +
            "  AND [System.State] NOT IN ('Closed','Removed','Done','Completed')",
        ["stale_sprint_check"] = "unused",
        ["missing_estimate_check"] = "unused",
        ["unassigned_check"] = "unused",
        ["release_pr_check"] = "unused",
        ["resolved_pr_check"] = "unused",
        ["tag_overview_check"] = "unused",
        ["pr_approval_check"] = "unused",
        ["stale_pr_check"] = "unused",
        ["unreviewed_pr_check"] = "unused",
    };

    public DashboardConfig LoadConfig()
    {
        lock (_lock)
        {
            if (!File.Exists(_configPath))
            {
                _cached = new DashboardConfig();
                _cachedMtime = DateTime.MinValue;
                return _cached;
            }

            var mtime = File.GetLastWriteTimeUtc(_configPath);
            if (_cached is not null && mtime == _cachedMtime)
                return _cached;

            var json = File.ReadAllText(_configPath);
            _cached = JsonSerializer.Deserialize<DashboardConfig>(json, JsonOpts) ?? new DashboardConfig();
            _cachedMtime = mtime;

            // Migrate legacy single-server fields to db_servers array
            if (_cached.DbServers.Count == 0 && !string.IsNullOrEmpty(_cached.DbServer))
            {
                _cached.DbServers.Add(new DbServerConfig
                {
                    Server = _cached.DbServer,
                    Port = _cached.DbPort,
                    Username = _cached.DbUsername,
                    Password = _cached.DbPassword,
                });
                _cached.DbServer = "";
                _cached.DbPort = 1433;
                _cached.DbUsername = "";
                _cached.DbPassword = "";
                // Persist the migration
                var migrated = JsonSerializer.Serialize(_cached, JsonOpts);
                File.WriteAllText(_configPath, migrated);
                _cachedMtime = File.GetLastWriteTimeUtc(_configPath);
            }

            return _cached;
        }
    }

    public void SaveConfig(DashboardConfig config)
    {
        var dir = Path.GetDirectoryName(_configPath)!;
        Directory.CreateDirectory(dir);

        // Auto-encrypt plaintext secrets before saving
        if (!string.IsNullOrEmpty(config.Pat) && !CryptoService.IsEncrypted(config.Pat))
            config.Pat = CryptoService.Encrypt(config.Pat);
        if (!string.IsNullOrEmpty(config.ApiKey) && !CryptoService.IsEncrypted(config.ApiKey))
            config.ApiKey = CryptoService.Encrypt(config.ApiKey);
        if (!string.IsNullOrEmpty(config.DbPassword) && !CryptoService.IsEncrypted(config.DbPassword))
            config.DbPassword = CryptoService.Encrypt(config.DbPassword);
        foreach (var srv in config.DbServers)
        {
            if (!string.IsNullOrEmpty(srv.Password) && !CryptoService.IsEncrypted(srv.Password))
                srv.Password = CryptoService.Encrypt(srv.Password);
        }

        var json = JsonSerializer.Serialize(config, JsonOpts);
        File.WriteAllText(_configPath, json);

        lock (_lock)
        {
            _cached = config;
            _cachedMtime = File.GetLastWriteTimeUtc(_configPath);
        }
    }

    // --- Project CRUD ---
    public List<ProjectConfig> ListProjects() => LoadConfig().Projects;

    public ProjectConfig? GetProject(string projectId)
        => LoadConfig().Projects.FirstOrDefault(p => p.Id == projectId);

    public ProjectConfig AddProject(ProjectConfig project)
    {
        var cfg = LoadConfig();
        project.Id = Guid.NewGuid().ToString("N")[..12];
        cfg.Projects.Add(project);
        SaveConfig(cfg);
        return project;
    }

    public ProjectConfig? UpdateProject(string projectId, ProjectConfig updated)
    {
        var cfg = LoadConfig();
        var idx = cfg.Projects.FindIndex(p => p.Id == projectId);
        if (idx < 0) return null;
        updated.Id = projectId;
        cfg.Projects[idx] = updated;
        SaveConfig(cfg);
        return updated;
    }

    public bool DeleteProject(string projectId)
    {
        var cfg = LoadConfig();
        var before = cfg.Projects.Count;
        cfg.Projects.RemoveAll(p => p.Id == projectId);
        if (cfg.Projects.Count < before)
        {
            SaveConfig(cfg);
            return true;
        }
        return false;
    }

    // --- DB Project CRUD ---
    public List<DbProjectConfig> ListDbProjects() => LoadConfig().DbProjects;

    public DbProjectConfig AddDbProject(DbProjectConfig project)
    {
        var cfg = LoadConfig();
        project.Id = Guid.NewGuid().ToString("N")[..12];
        cfg.DbProjects.Add(project);
        SaveConfig(cfg);
        return project;
    }

    public DbProjectConfig? UpdateDbProject(string id, DbProjectConfig updated)
    {
        var cfg = LoadConfig();
        var idx = cfg.DbProjects.FindIndex(p => p.Id == id);
        if (idx < 0) return null;
        updated.Id = id;
        cfg.DbProjects[idx] = updated;
        SaveConfig(cfg);
        return updated;
    }

    public bool DeleteDbProject(string id)
    {
        var cfg = LoadConfig();
        var before = cfg.DbProjects.Count;
        cfg.DbProjects.RemoveAll(p => p.Id == id);
        if (cfg.DbProjects.Count < before)
        {
            SaveConfig(cfg);
            return true;
        }
        return false;
    }

    // --- Audit Denylist (composite keys: "org/project_id") ---
    public List<string> GetAuditDenylist() => LoadConfig().AuditDenylist;

    public List<string> AddToAuditDenylist(string key)
    {
        var cfg = LoadConfig();
        if (!cfg.AuditDenylist.Contains(key))
        {
            cfg.AuditDenylist.Add(key);
            SaveConfig(cfg);
        }
        return cfg.AuditDenylist;
    }

    public List<string> RemoveFromAuditDenylist(string key)
    {
        var cfg = LoadConfig();
        cfg.AuditDenylist.Remove(key);
        SaveConfig(cfg);
        return cfg.AuditDenylist;
    }

    // --- Audit Configuration (group connections + rules) ---
    public AuditConfigResponse GetAuditConfig()
    {
        var cfg = LoadConfig();
        return new AuditConfigResponse
        {
            GroupConfig = cfg.AuditGroupConfig,
            Rules = cfg.AuditRules,
        };
    }

    public AuditConfigResponse SaveAuditConfig(AuditConfigInput input)
    {
        var cfg = LoadConfig();
        cfg.AuditGroupConfig = input.GroupConfig;
        cfg.AuditRules = input.Rules;
        SaveConfig(cfg);
        return new AuditConfigResponse
        {
            GroupConfig = cfg.AuditGroupConfig,
            Rules = cfg.AuditRules,
        };
    }

    // --- Roadmap Config CRUD ---
    public RoadmapConfig GetRoadmapConfig() => LoadConfig().Roadmap;

    public RoadmapConfig SaveRoadmapConfig(RoadmapConfig roadmap)
    {
        var cfg = LoadConfig();
        cfg.Roadmap = roadmap;
        SaveConfig(cfg);
        return cfg.Roadmap;
    }

    public RoadmapLaneConfig AddLane(RoadmapLaneConfig lane)
    {
        var cfg = LoadConfig();
        lane.Id = Guid.NewGuid().ToString("N")[..12];
        cfg.Roadmap.Lanes.Add(lane);
        SaveConfig(cfg);
        return lane;
    }

    public RoadmapLaneConfig? UpdateLane(string laneId, RoadmapLaneConfig updated)
    {
        var cfg = LoadConfig();
        var idx = cfg.Roadmap.Lanes.FindIndex(l => l.Id == laneId);
        if (idx < 0) return null;
        updated.Id = laneId;
        cfg.Roadmap.Lanes[idx] = updated;
        SaveConfig(cfg);
        return updated;
    }

    public bool DeleteLane(string laneId)
    {
        var cfg = LoadConfig();
        var before = cfg.Roadmap.Lanes.Count;
        cfg.Roadmap.Lanes.RemoveAll(l => l.Id == laneId);
        if (cfg.Roadmap.Lanes.Count < before)
        {
            SaveConfig(cfg);
            return true;
        }
        return false;
    }

    public List<RoadmapLaneConfig> ReorderLanes(List<string> laneIds)
    {
        var cfg = LoadConfig();
        var lookup = cfg.Roadmap.Lanes.ToDictionary(l => l.Id);
        var reordered = new List<RoadmapLaneConfig>();
        foreach (var id in laneIds)
        {
            if (lookup.TryGetValue(id, out var lane))
                reordered.Add(lane);
        }
        cfg.Roadmap.Lanes = reordered;
        SaveConfig(cfg);
        return cfg.Roadmap.Lanes;
    }

    public RoadmapProjectConfig AddRoadmapProject(RoadmapProjectConfig project)
    {
        var cfg = LoadConfig();
        cfg.Roadmap.Projects.Add(project);
        SaveConfig(cfg);
        return project;
    }

    public bool RemoveRoadmapProject(string organization, string projectId)
    {
        var cfg = LoadConfig();
        var before = cfg.Roadmap.Projects.Count;
        cfg.Roadmap.Projects.RemoveAll(p => p.Organization == organization && p.ProjectId == projectId);
        if (cfg.Roadmap.Projects.Count < before)
        {
            SaveConfig(cfg);
            return true;
        }
        return false;
    }

}
