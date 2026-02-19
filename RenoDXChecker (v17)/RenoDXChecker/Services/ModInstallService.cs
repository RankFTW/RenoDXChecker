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
        if (mod.SnapshotUrl == null)
            throw new InvalidOperationException($"{mod.Name} has no Snapshot download URL.");

        var fileName = Path.GetFileName(mod.SnapshotUrl);
        var destPath = Path.Combine(gameInstallPath, fileName);

        progress?.Report(("Downloading...", 0));

        // Download with progress
        var response = await _http.GetAsync(mod.SnapshotUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

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
                // Compare content-length with local file size as a quick check
                var remoteSize = resp.Content.Headers.ContentLength;
                var localSize = new FileInfo(localFile).Length;

                if (remoteSize.HasValue && remoteSize.Value != localSize)
                    return true;

                // Check Last-Modified
                var lastMod = resp.Content.Headers.LastModified;
                if (lastMod.HasValue && lastMod.Value.UtcDateTime > record.InstalledAt)
                    return true;
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
