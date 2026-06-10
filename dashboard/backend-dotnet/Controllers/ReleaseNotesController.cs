using System.Text.RegularExpressions;
using DashboardApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/release-notes")]
public partial class ReleaseNotesController : ControllerBase
{
    [GeneratedRegex(@"^\d+(\.\d+)*$")]
    private static partial Regex VersionPattern();

    private static List<ReleaseNoteEntry>? _cachedNotes;
    private static DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly object _cacheLock = new();

    [HttpGet]
    public async Task<IActionResult> GetReleaseNotes(CancellationToken ct)
    {
        lock (_cacheLock)
        {
            if (_cachedNotes is not null && DateTime.UtcNow < _cacheExpiry)
                return Ok(_cachedNotes);
        }

        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "dashboard", "release-notes"));
        if (!Directory.Exists(basePath))
            basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "release-notes"));
        if (!Directory.Exists(basePath))
            basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "release-notes"));
        if (!Directory.Exists(basePath))
            return Ok(Array.Empty<ReleaseNoteEntry>());

        var regex = VersionPattern();
        var entries = new List<ReleaseNoteEntry>();

        foreach (var dir in Directory.GetDirectories(basePath))
        {
            var version = Path.GetFileName(dir);
            if (!regex.IsMatch(version))
                continue;

            var notesPath = Path.GetFullPath(Path.Combine(dir, "notes.md"));
            if (!notesPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!System.IO.File.Exists(notesPath))
                continue;

            var content = await System.IO.File.ReadAllTextAsync(notesPath, ct);
            entries.Add(new ReleaseNoteEntry(version, content));
        }

        entries.Sort((a, b) => CompareVersionsDescending(a.Version, b.Version));

        lock (_cacheLock)
        {
            _cachedNotes = entries;
            _cacheExpiry = DateTime.UtcNow.AddHours(1);
        }

        return Ok(entries);
    }

    private static int CompareVersionsDescending(string a, string b)
    {
        var partsA = a.Split('.').Select(int.Parse).ToArray();
        var partsB = b.Split('.').Select(int.Parse).ToArray();
        var len = Math.Max(partsA.Length, partsB.Length);
        for (var i = 0; i < len; i++)
        {
            var va = i < partsA.Length ? partsA[i] : 0;
            var vb = i < partsB.Length ? partsB[i] : 0;
            if (vb != va) return vb.CompareTo(va);
        }
        return 0;
    }
}
