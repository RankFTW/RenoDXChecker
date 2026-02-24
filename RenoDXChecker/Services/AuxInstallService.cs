using System.Text.Json;
using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>
/// Installs and manages Display Commander and ReShade for each game.
/// Maintains its own DB (aux_installed.json) separate from RenoDX records.
/// Caches downloaded files in the same downloads folder as RenoDX.
/// </summary>
public class AuxInstallService
{
    // ── URLs & filenames ──────────────────────────────────────────────────────────
    public const string DcUrl        = "https://github.com/pmnoxx/display-commander/releases/download/latest_build/zzz_display_commander.addon64";
    public const string DcCacheFile  = "zzz_display_commander.addon64";
    public const string DcNormalName = "zzz_display_commander.addon64";
    public const string DcDxgiName   = "dxgi.dll";

    // ReShade is bundled alongside the app exe (ReShade64.dll / ReShade32.dll).
    // On first install the DLLs are copied into the staging folder and used from there.
    // If the staging copies are deleted, they are restored from the app bundle automatically.
    public const string RsNormalName  = "dxgi.dll";       // install name when DC Mode OFF
    public const string RsDcModeName  = "ReShade64.dll";  // install name when DC Mode ON
    public const string RsBundle64    = "ReShade64.dll";  // filename next to the app exe
    public const string RsBundle32    = "ReShade32.dll";  // 32-bit variant (bundled, not currently installed)
    public const string RsStaged64    = "ReShade64.dll";  // filename inside staging folder
    public const string RsStaged32    = "ReShade32.dll";

    // Staging folder: %LocalAppData%\RenoDXCommander\reshade\
    public static readonly string RsStagingDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXCommander", "reshade");
    public static string RsStagedPath64 => Path.Combine(RsStagingDir, RsStaged64);
    public static string RsStagedPath32 => Path.Combine(RsStagingDir, RsStaged32);

    /// <summary>
    /// Ensures the staged ReShade DLLs exist. Copies them from the app bundle
    /// (the folder containing the running exe) if they are missing or empty.
    /// Safe to call every time before an install.
    /// </summary>
    public static void EnsureReShadeStaging()
    {
        Directory.CreateDirectory(RsStagingDir);

        var appDir = AppContext.BaseDirectory;
        foreach (var (bundle, staged) in new[] {
            (Path.Combine(appDir, RsBundle64), RsStagedPath64),
            (Path.Combine(appDir, RsBundle32), RsStagedPath32),
        })
        {
            if (File.Exists(bundle) &&
                (!File.Exists(staged) || new FileInfo(staged).Length == 0))
            {
                File.Copy(bundle, staged, overwrite: true);
            }
        }
    }

    // Keys used in AuxInstalledRecord.AddonType
    public const string TypeDc      = "DisplayCommander";
    public const string TypeReShade = "ReShade";

    // ── Infrastructure ────────────────────────────────────────────────────────────
    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXCommander", "aux_installed.json");

    // ── INI preset folder ─────────────────────────────────────────────────────────
    /// <summary>
    /// User-placed preset config files live here.
    /// Place reshade.ini here to enable the 📋 INI button on the ReShade row.
    /// Place DisplayCommander.toml here to enable the 📋 INI button on the DC row.
    /// </summary>
    public static readonly string InisDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXCommander", "inis");

    public static string RsIniPath => Path.Combine(InisDir, "reshade.ini");
    public static string DcIniPath => Path.Combine(InisDir, "DisplayCommander.toml");

    /// <summary>Ensures the inis directory exists (call on startup).</summary>
    public static void EnsureInisDir() => Directory.CreateDirectory(InisDir);

    /// <summary>Copies reshade.ini from the inis folder to the given game directory.</summary>
    public static void CopyRsIni(string gameDir)
    {
        if (!File.Exists(RsIniPath))
            throw new FileNotFoundException("reshade.ini not found in inis folder.", RsIniPath);
        File.Copy(RsIniPath, Path.Combine(gameDir, "reshade.ini"), overwrite: true);
    }

    /// <summary>Copies DisplayCommander.toml from the inis folder to the given game directory.</summary>
    public static void CopyDcIni(string gameDir)
    {
        if (!File.Exists(DcIniPath))
            throw new FileNotFoundException("DisplayCommander.toml not found in inis folder.", DcIniPath);
        File.Copy(DcIniPath, Path.Combine(gameDir, "DisplayCommander.toml"), overwrite: true);
    }

    public static readonly string DownloadCacheDir = ModInstallService.DownloadCacheDir;

    private readonly HttpClient _http;

    public AuxInstallService(HttpClient http) => _http = http;

    // ── Install — Display Commander ───────────────────────────────────────────────

    public async Task<AuxInstalledRecord> InstallDcAsync(
        string gameName,
        string installPath,
        bool dcMode,
        IProgress<(string message, double percent)>? progress = null)
    {
        Directory.CreateDirectory(DownloadCacheDir);

        var cachePath = Path.Combine(DownloadCacheDir, DcCacheFile);
        var destName     = dcMode ? DcDxgiName    : DcNormalName;
        var opposingName = dcMode ? DcNormalName  : DcDxgiName;
        var destPath     = Path.Combine(installPath, destName);
        // Remove any file installed under the opposing mode's name
        var opposingPath = Path.Combine(installPath, opposingName);
        if (File.Exists(opposingPath)) try { File.Delete(opposingPath); } catch { }

        // ── HEAD for remote size ──────────────────────────────────────────────────
        long? remoteSize = null;
        try
        {
            var head = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, DcUrl));
            if (head.IsSuccessStatusCode)
                remoteSize = head.Content.Headers.ContentLength;
        }
        catch { }

        // ── Cache check ───────────────────────────────────────────────────────────
        bool usedCache = false;
        if (File.Exists(cachePath))
        {
            var cacheSize = new FileInfo(cachePath).Length;
            if (!remoteSize.HasValue || remoteSize.Value == cacheSize)
            {
                progress?.Report(("Installing Display Commander from cache...", 50));
                File.Copy(cachePath, destPath, overwrite: true);
                usedCache = true;
                progress?.Report(("Display Commander installed!", 100));
            }
        }

        // ── Download ──────────────────────────────────────────────────────────────
        if (!usedCache)
        {
            progress?.Report(("Downloading Display Commander...", 0));
            var response = await _http.GetAsync(DcUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Display Commander download failed: {response.StatusCode}");

            remoteSize ??= response.Content.Headers.ContentLength;
            var total      = remoteSize ?? -1L;
            long downloaded = 0;
            var buffer     = new byte[81920];
            var tempPath   = cachePath + ".tmp";

            using (var net  = await response.Content.ReadAsStreamAsync())
            using (var file = File.Create(tempPath))
            {
                int read;
                while ((read = await net.ReadAsync(buffer)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;
                    if (total > 0)
                        progress?.Report(($"Downloading DC... {downloaded / 1024} KB",
                                          (double)downloaded / total * 100));
                }
            }

            if (File.Exists(cachePath)) File.Delete(cachePath);
            File.Move(tempPath, cachePath);
            if (!remoteSize.HasValue)
                remoteSize = new FileInfo(cachePath).Length;

            File.Copy(cachePath, destPath, overwrite: true);
        }

        var record = new AuxInstalledRecord
        {
            GameName       = gameName,
            InstallPath    = installPath,
            AddonType      = TypeDc,
            InstalledAs    = destName,
            SourceUrl      = DcUrl,
            RemoteFileSize = remoteSize,
            InstalledAt    = DateTime.UtcNow,
        };
        SaveRecord(record);
        return record;
    }

    // ── Install — ReShade ─────────────────────────────────────────────────────────

    /// <summary>
    /// Scrapes reshade.me to find the latest addon-support installer URL.
    /// Returns the fallback URL if parsing fails so installs never hard-error.
    /// </summary>

    public async Task<AuxInstalledRecord> InstallReShadeAsync(
        string gameName,
        string installPath,
        bool dcMode,
        IProgress<(string message, double percent)>? progress = null)
    {
        Directory.CreateDirectory(DownloadCacheDir);

        var destName = dcMode ? RsDcModeName : RsNormalName;
        var destPath = Path.Combine(installPath, destName);

        // Remove the opposing-mode ReShade file if DC Mode is OFF
        // (when OFF, old DC-mode ReShade64.dll can be safely replaced).
        // Never touch dxgi.dll here — it may belong to DC.
        if (!dcMode)
        {
            var rsOldPath = Path.Combine(installPath, RsDcModeName);
            if (File.Exists(rsOldPath)) try { File.Delete(rsOldPath); } catch { }
        }

        // ── Ensure staged DLLs exist (restore from app bundle if missing) ────────
        progress?.Report(("Preparing ReShade files...", 10));
        EnsureReShadeStaging();

        if (!File.Exists(RsStagedPath64))
            throw new FileNotFoundException(
                "ReShade64.dll was not found in the app folder or staging directory.\n" +
                $"Expected: {RsStagedPath64}\n" +
                "Please ensure ReShade64.dll is placed next to RenoDXCommander.exe.");

        // ── Copy staged DLL to game folder ────────────────────────────────────────
        progress?.Report(("Installing ReShade...", 80));
        File.Copy(RsStagedPath64, destPath, overwrite: true);
        progress?.Report(("ReShade installed!", 100));

        var record = new AuxInstalledRecord
        {
            GameName       = gameName,
            InstallPath    = installPath,
            AddonType      = TypeReShade,
            InstalledAs    = destName,
            SourceUrl      = null,       // bundled — no remote URL
            RemoteFileSize = null,       // no remote size to track
            InstalledAt    = DateTime.UtcNow,
        };
        SaveRecord(record);
        return record;
    }

    // ── Update detection ──────────────────────────────────────────────────────────

    public async Task<bool> CheckForUpdateAsync(AuxInstalledRecord record)
    {
        if (record.SourceUrl == null) return false;

        var localFile = Path.Combine(record.InstallPath, record.InstalledAs);
        if (!File.Exists(localFile)) return true;

        try
        {
            var resp = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, record.SourceUrl));
            if (!resp.IsSuccessStatusCode) return false;

            var remote = resp.Content.Headers.ContentLength;
            if (!remote.HasValue) return false;

            if (record.RemoteFileSize.HasValue)
                return remote.Value != record.RemoteFileSize.Value;

            return remote.Value != new FileInfo(localFile).Length;
        }
        catch { return false; }
    }

    // ── Uninstall ─────────────────────────────────────────────────────────────────

    public void Uninstall(AuxInstalledRecord record)
    {
        var path = Path.Combine(record.InstallPath, record.InstalledAs);
        if (File.Exists(path)) File.Delete(path);
        RemoveRecord(record);
    }

    // ── DB ────────────────────────────────────────────────────────────────────────

    public List<AuxInstalledRecord> LoadAll() => LoadDb();

    public AuxInstalledRecord? FindRecord(string gameName, string installPath, string addonType)
    {
        return LoadDb().FirstOrDefault(r =>
            r.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase) &&
            r.InstallPath.Equals(installPath, StringComparison.OrdinalIgnoreCase) &&
            r.AddonType.Equals(addonType, StringComparison.OrdinalIgnoreCase));
    }

    private void SaveRecord(AuxInstalledRecord record)
    {
        var db = LoadDb();
        var i  = db.FindIndex(r =>
            r.GameName.Equals(record.GameName, StringComparison.OrdinalIgnoreCase) &&
            r.InstallPath.Equals(record.InstallPath, StringComparison.OrdinalIgnoreCase) &&
            r.AddonType.Equals(record.AddonType, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) db[i] = record; else db.Add(record);
        SaveDb(db);
    }

    public void RemoveRecord(AuxInstalledRecord record)
    {
        var db = LoadDb();
        db.RemoveAll(r =>
            r.GameName.Equals(record.GameName, StringComparison.OrdinalIgnoreCase) &&
            r.InstallPath.Equals(record.InstallPath, StringComparison.OrdinalIgnoreCase) &&
            r.AddonType.Equals(record.AddonType, StringComparison.OrdinalIgnoreCase));
        SaveDb(db);
    }

    private List<AuxInstalledRecord> LoadDb()
    {
        try
        {
            if (!File.Exists(DbPath)) return new();
            return JsonSerializer.Deserialize<List<AuxInstalledRecord>>(File.ReadAllText(DbPath)) ?? new();
        }
        catch { return new(); }
    }

    private void SaveDb(List<AuxInstalledRecord> db)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        File.WriteAllText(DbPath, JsonSerializer.Serialize(db,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
