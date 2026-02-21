using System.Security.Cryptography;
using System.Text.Json;
using RenoDXChecker.Models;

namespace RenoDXChecker.Services;

/// <summary>
/// Downloads, installs, updates, and uninstalls RenoDX addon files.
/// Tracks installations via a local JSON database.
///
/// Download cache: files go to %LocalAppData%\RenoDXChecker\downloads\ so
/// reinstalling or installing the same addon on another game skips the download.
///
/// Update detection: stores RemoteFileSize at install time and compares against
/// the current remote Content-Length — stable across relaunches regardless of
/// local filesystem behaviour or CDN edge-server variation.
/// </summary>
public class ModInstallService
{
    public event Action<InstalledModRecord>? InstallCompleted;

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXChecker", "installed.json");

    public static readonly string DownloadCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXChecker", "downloads");

    private readonly HttpClient _http;

    public ModInstallService(HttpClient http) => _http = http;

    // ── Install ───────────────────────────────────────────────────────────────────

    public async Task<InstalledModRecord> InstallAsync(
        GameMod mod,
        string gameInstallPath,
        IProgress<(string message, double percent)>? progress = null)
    {
        if (mod.SnapshotUrl == null)
            throw new InvalidOperationException($"{mod.Name} has no Snapshot download URL.");

        Directory.CreateDirectory(DownloadCacheDir);

        var fileName  = Path.GetFileName(mod.SnapshotUrl);
        var destPath  = Path.Combine(gameInstallPath, fileName);
        var cachePath = Path.Combine(DownloadCacheDir, fileName);

        // ── Step 1: get remote Content-Length (single HEAD) ───────────────────────
        long? remoteSize = null;
        try
        {
            var headResp = await _http.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, mod.SnapshotUrl));
            if (headResp.IsSuccessStatusCode)
                remoteSize = headResp.Content.Headers.ContentLength;
        }
        catch { /* network issue — proceed without size */ }

        // ── Step 2: use cache if it matches remote size (or size unknown) ─────────
        bool usedCache = false;
        if (File.Exists(cachePath))
        {
            var cacheSize = new FileInfo(cachePath).Length;
            bool sizeOk   = !remoteSize.HasValue || remoteSize.Value == cacheSize;
            if (sizeOk)
            {
                progress?.Report(("Installing from cache...", 50));
                File.Copy(cachePath, destPath, overwrite: true);
                usedCache = true;
                progress?.Report(("Installed from cache!", 100));
            }
        }

        // ── Step 3: fresh download if no usable cache ─────────────────────────────
        if (!usedCache)
        {
            progress?.Report(("Downloading...", 0));
            HttpResponseMessage? response = null;
            var tried      = new List<string>();
            var candidates = new List<string> { mod.SnapshotUrl };
            try
            {
                var uri = new Uri(mod.SnapshotUrl);
                var fn  = Path.GetFileName(uri.LocalPath);

                if (fn.Equals("renodx-unityengine.addon64", StringComparison.OrdinalIgnoreCase)
                 || fn.Equals("renodx-unityengine.addon32", StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add($"https://notvoosh.github.io/renodx-unity/{fn}");
                    candidates.Add($"https://clshortfuse.github.io/renodx/{fn}");
                    candidates.Add($"https://github.com/clshortfuse/renodx/releases/download/snapshot/{fn}");
                }

                if (uri.Host.EndsWith("github.io", StringComparison.OrdinalIgnoreCase))
                {
                    var fn2 = Path.GetFileName(uri.LocalPath);
                    if (!string.IsNullOrEmpty(fn2))
                        candidates.Add($"https://github.com/clshortfuse/renodx/releases/download/snapshot/{fn2}");
                }
            }
            catch { }

            foreach (var url in candidates.Where(u => !string.IsNullOrEmpty(u)).Distinct())
            {
                tried.Add(url);
                try
                {
                    response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode) break;
                }
                catch { }
            }

            if (response == null || !response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Failed to download snapshot. Tried: {string.Join(", ", tried)}");

            // Capture size from actual download response if HEAD didn't return one
            if (!remoteSize.HasValue)
                remoteSize = response.Content.Headers.ContentLength;

            var total      = remoteSize ?? -1L;
            var buffer     = new byte[81920];
            long downloaded = 0;

            // Download into cache, then copy to game folder
            var tempPath = cachePath + ".tmp";
            using (var netStream = await response.Content.ReadAsStreamAsync())
            using (var cacheFile = File.Create(tempPath))
            {
                int read;
                while ((read = await netStream.ReadAsync(buffer)) > 0)
                {
                    await cacheFile.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;
                    if (total > 0)
                        progress?.Report(($"Downloading... {downloaded / 1024} KB",
                                          (double)downloaded / total * 100));
                }
                cacheFile.Flush();
            }

            if (File.Exists(cachePath)) File.Delete(cachePath);
            File.Move(tempPath, cachePath);
            File.Copy(cachePath, destPath, overwrite: true);

            // Record actual downloaded size if we didn't have it from HEAD
            if (!remoteSize.HasValue)
                remoteSize = new FileInfo(cachePath).Length;
        }

        var hash = await ComputeHashAsync(destPath);

        var record = new InstalledModRecord
        {
            GameName       = mod.Name,
            InstallPath    = gameInstallPath,
            AddonFileName  = fileName,
            FileHash       = hash,
            InstalledAt    = DateTime.UtcNow,
            SnapshotUrl    = mod.SnapshotUrl,
            RemoteFileSize = remoteSize,   // ← stored for stable update detection
        };
        SaveRecord(record);
        try { InstallCompleted?.Invoke(record); } catch { }
        progress?.Report(("Installed!", 100));
        return record;
    }

    // ── Update detection ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the remote snapshot is newer than what was installed.
    ///
    /// Strategy (ordered by reliability):
    ///   1. Missing local file → always reinstall.
    ///   2. RemoteFileSize stored → compare current remote Content-Length against it.
    ///      This is stable because the stored value came from the actual download,
    ///      not from the local file copy (which can differ due to FS/copy behaviour).
    ///   3. No stored size → compare remote Content-Length against local file size.
    ///      Less reliable but better than nothing.
    ///   4. No Content-Length from server → assume no update (avoid false positives).
    /// </summary>
    public async Task<bool> CheckForUpdateAsync(InstalledModRecord record)
    {
        if (record.SnapshotUrl == null) return false;

        var localFile = Path.Combine(record.InstallPath, record.AddonFileName);
        if (!File.Exists(localFile)) return true;

        try
        {
            var req  = new HttpRequestMessage(HttpMethod.Head, record.SnapshotUrl);
            var resp = await _http.SendAsync(req);

            if (resp.IsSuccessStatusCode)
            {
                var currentRemoteSize = resp.Content.Headers.ContentLength;
                if (!currentRemoteSize.HasValue) return false; // can't tell → no update

                if (record.RemoteFileSize.HasValue)
                {
                    // Best path: compare stored install-time size vs current remote size
                    return currentRemoteSize.Value != record.RemoteFileSize.Value;
                }
                else
                {
                    // Fallback for records installed before RemoteFileSize was added:
                    // compare remote size vs local file size
                    var localSize = new FileInfo(localFile).Length;
                    return currentRemoteSize.Value != localSize;
                }
            }
        }
        catch { /* network issue — assume no update */ }

        return false;
    }

    // ── Uninstall ─────────────────────────────────────────────────────────────────

    public void Uninstall(InstalledModRecord record)
    {
        var filePath = Path.Combine(record.InstallPath, record.AddonFileName);
        if (File.Exists(filePath)) File.Delete(filePath);
        // Cache copy intentionally kept for future reinstalls.

        var db = LoadDb();
        db.RemoveAll(r => r.GameName == record.GameName && r.InstallPath == record.InstallPath);
        SaveDb(db);
    }

    // ── Database ──────────────────────────────────────────────────────────────────

    public List<InstalledModRecord> LoadAll() => LoadDb();

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
        var i  = db.FindIndex(r =>
            r.GameName == record.GameName &&
            r.InstallPath.Equals(record.InstallPath, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) db[i] = record; else db.Add(record);
        SaveDb(db);
    }

    private List<InstalledModRecord> LoadDb()
    {
        try
        {
            if (!File.Exists(DbPath)) return new();
            return JsonSerializer.Deserialize<List<InstalledModRecord>>(File.ReadAllText(DbPath)) ?? new();
        }
        catch { return new(); }
    }

    private void SaveDb(List<InstalledModRecord> db)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        File.WriteAllText(DbPath, JsonSerializer.Serialize(db,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<string> ComputeHashAsync(string path)
    {
        using var sha    = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(await sha.ComputeHashAsync(stream));
    }

    public static bool IsValidGameFolder(string path) =>
        Directory.Exists(path) && Directory.GetFiles(path, "*.exe").Length > 0;
}
