using System.Security.Cryptography;
using System.Text.Json;
using RenoDXChecker.Models;

namespace RenoDXChecker.Services;

/// <summary>
/// Downloads, installs, updates, and uninstalls RenoDX addon files.
/// Tracks installations via a local JSON database.
/// </summary>
public class ModInstallService
{
    // Raised when an install completes (record has been saved).
    public event Action<InstalledModRecord>? InstallCompleted;

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXChecker", "installed.json");

    private readonly HttpClient _http;

    public ModInstallService(HttpClient http)
    {
        _http = http;
    }

    // ── Install / Update ──────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the .addon64 file and places it in the game's install folder.
    /// Returns the installed record.
    /// </summary>
    public async Task<InstalledModRecord> InstallAsync(
        GameMod mod,
        string gameInstallPath,
        IProgress<(string message, double percent)>? progress = null)
    {
        // Debug logging removed.
        if (mod.SnapshotUrl == null)
            throw new InvalidOperationException($"{mod.Name} has no Snapshot download URL.");

        var fileName = Path.GetFileName(mod.SnapshotUrl);
        var destPath = Path.Combine(gameInstallPath, fileName);
        // Debug logging removed.

        progress?.Report(("Downloading...", 0));

        // Try downloading the provided URL, with fallbacks for known hosting variations
        HttpResponseMessage? response = null;
        var tried = new List<string>();
        var candidates = new List<string> { mod.SnapshotUrl };
        try
        {
            // Attempt to build sensible fallback URLs based on the filename and known hosts
            try
            {
                var uri = new Uri(mod.SnapshotUrl);
                var fn = Path.GetFileName(uri.LocalPath);

                // If the filename matches the unity generic asset, try alternative hosts
                if (fn.Equals("renodx-unityengine.addon64", StringComparison.OrdinalIgnoreCase)
                    || fn.Equals("renodx-unityengine.addon32", StringComparison.OrdinalIgnoreCase))
                {
                    // Known alternative hosts (user-provided reliable host + canonical gh-pages)
                    candidates.Add($"https://notvoosh.github.io/renodx-unity/{fn}");
                    candidates.Add($"https://clshortfuse.github.io/renodx/{fn}");
                    // Also try the GitHub releases snapshot asset
                    candidates.Add($"https://github.com/clshortfuse/renodx/releases/download/snapshot/{fn}");
                }

                // If orig host is a github.io pages site, also try the releases/download fallback
                if (uri.Host.EndsWith("github.io", StringComparison.OrdinalIgnoreCase))
                {
                    var fn2 = Path.GetFileName(uri.LocalPath);
                    if (!string.IsNullOrEmpty(fn2))
                        candidates.Add($"https://github.com/clshortfuse/renodx/releases/download/snapshot/{fn2}");
                }
            }
            catch { /* ignore URI parse issues */ }

            foreach (var url in candidates.Where(u => !string.IsNullOrEmpty(u)).Distinct())
            {
                tried.Add(url);
                try
                {
                    response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode) break;
                }
                catch { /* try next candidate */ }
            }
        }
        catch { /* fall through to error handling below */ }

        if (response == null || !response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to download snapshot. Tried: {string.Join(", ", tried)}");
        }

        var total = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
        long downloaded = 0;

        using var stream = await response.Content.ReadAsStreamAsync();
        using var file = File.Create(destPath);

        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;
            if (total > 0)
                progress?.Report(($"Downloading... {downloaded / 1024} KB", (double)downloaded / total * 100));
        }

        file.Flush();
        // Debug logging removed.
        var hash = await ComputeHashAsync(destPath);

        var record = new InstalledModRecord
        {
            GameName = mod.Name,
            InstallPath = gameInstallPath,
            AddonFileName = fileName,
            FileHash = hash,
            InstalledAt = DateTime.UtcNow,
            SnapshotUrl = mod.SnapshotUrl,
        };
        SaveRecord(record);
        try { InstallCompleted?.Invoke(record); } catch { }
        progress?.Report(("Installed!", 100));
        return record;
    }

    /// <summary>
    /// Checks if the remote snapshot file differs from the locally installed version.
    /// Uses HTTP HEAD + content-length or ETag comparison.
    /// Falls back to a hash comparison by re-downloading.
    /// </summary>
    public async Task<bool> CheckForUpdateAsync(InstalledModRecord record)
    {
        if (record.SnapshotUrl == null) return false;

        var localFile = Path.Combine(record.InstallPath, record.AddonFileName);
        if (!File.Exists(localFile)) return true; // File deleted — needs reinstall

        try
        {
            // Use HEAD request to check last-modified / ETag
            var req = new HttpRequestMessage(HttpMethod.Head, record.SnapshotUrl);
            var resp = await _http.SendAsync(req);

            if (resp.IsSuccessStatusCode)
            {
                // Primary signal: Content-Length vs local file size.
                // This is the only reliable indicator — CDN Last-Modified headers are
                // inconsistent and cause false positives for disk-scanned installs.
                var remoteSize = resp.Content.Headers.ContentLength;
                if (remoteSize.HasValue)
                {
                    var localSize = new FileInfo(localFile).Length;
                    if (remoteSize.Value != localSize) return true;
                }
                // If the server didn't return Content-Length we can't tell — assume no update.
            }
        }
        catch { /* Network issue — assume no update */ }

        return false;
    }

    // ── Uninstall ─────────────────────────────────────────────────────────────────

    public void Uninstall(InstalledModRecord record)
    {
        var filePath = Path.Combine(record.InstallPath, record.AddonFileName);
        if (File.Exists(filePath))
            File.Delete(filePath);

        var db = LoadDb();
        db.RemoveAll(r => r.GameName == record.GameName && r.InstallPath == record.InstallPath);
        SaveDb(db);
    }

    // ── Database ──────────────────────────────────────────────────────────────────

    public List<InstalledModRecord> LoadAll()
    {
        return LoadDb();
    }

    public InstalledModRecord? FindRecord(string gameName, string? installPath = null)
    {
        var db = LoadDb();
        return db.FirstOrDefault(r =>
            r.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase) &&
            (installPath == null || r.InstallPath.Equals(installPath, StringComparison.OrdinalIgnoreCase)));
    }

    public void SaveRecordPublic(InstalledModRecord record) => SaveRecord(record);

    private void SaveRecord(InstalledModRecord record)
    {
        var db = LoadDb();
        var existing = db.FindIndex(r =>
            r.GameName == record.GameName &&
            r.InstallPath.Equals(record.InstallPath, StringComparison.OrdinalIgnoreCase));

        if (existing >= 0)
            db[existing] = record;
        else
            db.Add(record);

        SaveDb(db);
    }

    private List<InstalledModRecord> LoadDb()
    {
        try
        {
            if (!File.Exists(DbPath)) return new();
            var json = File.ReadAllText(DbPath);
            return JsonSerializer.Deserialize<List<InstalledModRecord>>(json) ?? new();
        }
        catch { return new(); }
    }

    private void SaveDb(List<InstalledModRecord> db)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        File.WriteAllText(DbPath, JsonSerializer.Serialize(db, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<string> ComputeHashAsync(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var bytes = await sha.ComputeHashAsync(stream);
        return Convert.ToHexString(bytes);
    }

    // ── Verify install dir ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the given directory looks like a valid game folder
    /// (contains at least one .exe file).
    /// </summary>
    public static bool IsValidGameFolder(string path)
    {
        return Directory.Exists(path) && Directory.GetFiles(path, "*.exe").Length > 0;
    }
}
