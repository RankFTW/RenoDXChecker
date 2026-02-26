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
    public const string DcUrl32      = "https://github.com/pmnoxx/display-commander/releases/download/latest_build/zzz_display_commander.addon32";
    public const string DcCacheFile  = "zzz_display_commander.addon64";
    public const string DcCacheFile32 = "zzz_display_commander.addon32";
    public const string DcNormalName = "zzz_display_commander.addon64";
    public const string DcNormalName32 = "zzz_display_commander.addon32";
    public const string DcDxgiName   = "dxgi.dll";

    // ReShade is bundled alongside the app exe (ReShade64.dll / ReShade32.dll).
    // On first install the DLLs are copied into the staging folder and used from there.
    // If the staging copies are deleted, they are restored from the app bundle automatically.
    public const string RsNormalName  = "dxgi.dll";       // install name when DC Mode OFF
    public const string RsDcModeName  = "ReShade64.dll";  // install name when DC Mode ON (64-bit)
    public const string RsDcModeName32 = "ReShade32.dll"; // install name when DC Mode ON (32-bit)
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

    /// <summary>
    /// Classifies what a dxgi.dll file is based on its content.
    /// </summary>
    public enum DxgiFileType { Unknown, ReShade, DisplayCommander }

    /// <summary>
    /// Identifies what type of dxgi.dll is at the given path.
    /// Uses strict checks: exact size match against known staged/cached binaries,
    /// and binary string scanning for definitive markers.
    /// Returns Unknown unless there is positive evidence — never guesses based on size alone.
    /// </summary>
    public static DxgiFileType IdentifyDxgiFile(string filePath)
    {
        if (!File.Exists(filePath)) return DxgiFileType.Unknown;

        if (IsReShadeFileStrict(filePath)) return DxgiFileType.ReShade;
        if (IsDcFileStrict(filePath))      return DxgiFileType.DisplayCommander;

        return DxgiFileType.Unknown;
    }

    /// <summary>
    /// Strict ReShade check: exact size match against staged ReShade DLLs,
    /// OR binary scan for ReShade-specific strings. Never falls back to a size threshold.
    /// </summary>
    public static bool IsReShadeFileStrict(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        var fileSize = new FileInfo(filePath).Length;

        // Exact size match against staged ReShade64.dll
        if (File.Exists(RsStagedPath64) && fileSize == new FileInfo(RsStagedPath64).Length)
            return true;
        // Exact size match against staged ReShade32.dll
        if (File.Exists(RsStagedPath32) && fileSize == new FileInfo(RsStagedPath32).Length)
            return true;

        // Binary string scan for ReShade markers
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var text = System.Text.Encoding.ASCII.GetString(bytes);
            // ReShade embeds its own name in the DLL
            if (text.Contains("ReShade", StringComparison.Ordinal) &&
                (text.Contains("reshade.me", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("crosire", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("ReShade DLL", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Strict DC check: exact size match against cached DC binaries,
    /// OR binary scan for DC-specific strings. Never guesses based on size alone.
    /// </summary>
    public static bool IsDcFileStrict(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        var fileSize = new FileInfo(filePath).Length;

        // Exact size match against cached DC files
        try
        {
            var dc64Cache = Path.Combine(DownloadCacheDir, DcCacheFile);
            var dc32Cache = Path.Combine(DownloadCacheDir, DcCacheFile32);
            if (File.Exists(dc64Cache) && fileSize == new FileInfo(dc64Cache).Length)
                return true;
            if (File.Exists(dc32Cache) && fileSize == new FileInfo(dc32Cache).Length)
                return true;
        }
        catch { }

        // Binary string scan for DC-specific markers
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var text = System.Text.Encoding.ASCII.GetString(bytes);
            if (text.Contains("display_commander", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("DisplayCommander", StringComparison.Ordinal))
                return true;
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Returns true if the file at <paramref name="filePath"/> is a ReShade DLL.
    /// Uses exact size match against staged copies first, then binary string scan.
    /// Falls back to a 2 MB size threshold ONLY if neither staged copies nor
    /// binary markers are available — this is the legacy heuristic and is less reliable.
    /// </summary>
    public static bool IsReShadeFile(string filePath)
    {
        if (!File.Exists(filePath)) return false;

        // Prefer the strict check which requires positive evidence
        if (IsReShadeFileStrict(filePath)) return true;

        // Legacy fallback: if no staged copies exist AND binary scan didn't find markers,
        // use the size heuristic. This only triggers on first run before staging.
        var fileSize = new FileInfo(filePath).Length;
        bool hasStagedCopies = File.Exists(RsStagedPath64) || File.Exists(RsStagedPath32);
        if (!hasStagedCopies && fileSize > 2 * 1024 * 1024)
            return true;

        return false;
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

    /// <summary>
    /// Ensures the inis directory exists and seeds the default reshade.ini if missing.
    /// The bundled ReShade.ini is copied from the app directory on first run or whenever
    /// the file is absent — an existing user-modified reshade.ini is never overwritten.
    /// </summary>
    public static void EnsureInisDir()
    {
        Directory.CreateDirectory(InisDir);

        // Seed bundled reshade.ini if the user doesn't already have one
        if (!File.Exists(RsIniPath))
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "ReShade.ini");
            if (File.Exists(bundled))
            {
                try
                {
                    File.Copy(bundled, RsIniPath, overwrite: false);
                    CrashReporter.Log("EnsureInisDir: seeded default reshade.ini from bundle");
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"EnsureInisDir: failed to seed reshade.ini — {ex.Message}");
                }
            }
        }
    }

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
        AuxInstalledRecord? existingDcRecord = null,
        AuxInstalledRecord? existingRsRecord = null,
        string? shaderModeOverride = null,
        bool use32Bit = false,
        IProgress<(string message, double percent)>? progress = null)
    {
        Directory.CreateDirectory(DownloadCacheDir);

        var activeUrl    = use32Bit ? DcUrl32       : DcUrl;
        var cachePath    = Path.Combine(DownloadCacheDir, use32Bit ? DcCacheFile32 : DcCacheFile);
        var destName     = dcMode ? DcDxgiName : (use32Bit ? DcNormalName32 : DcNormalName);
        var opposingName = dcMode ? (use32Bit ? DcNormalName32 : DcNormalName) : DcDxgiName;
        var destPath     = Path.Combine(installPath, destName);

        // ── Mode-switch cleanup ───────────────────────────────────────────────────
        // Remove the previous DC-mode file only if DC itself placed it.
        // CRITICAL: never delete dxgi.dll if ReShade currently owns it.
        //   DC Mode OFF → new dest = zzz_display_commander.addon(32|64), opposing = dxgi.dll
        //                 dxgi.dll may be ReShade's — only remove if DC's own record says it put it there.
        //   DC Mode ON  → new dest = dxgi.dll, opposing = zzz_display_commander.addon(32|64)
        //                 safe to remove the old zzz file (it's always DC's).
        // Also clean up the opposite-bitness opposing file when DC mode is on.
        var opposingPath = Path.Combine(installPath, opposingName);
        bool opposingBelongsToDc = existingDcRecord != null
            && existingDcRecord.InstalledAs.Equals(opposingName, StringComparison.OrdinalIgnoreCase);
        bool opposingBelongsToRs = existingRsRecord != null
            && existingRsRecord.InstalledAs.Equals(opposingName, StringComparison.OrdinalIgnoreCase);
        if (opposingBelongsToDc && !opposingBelongsToRs && File.Exists(opposingPath))
        {
            try { File.Delete(opposingPath); } catch { }
        }

        // ── HEAD for remote size ──────────────────────────────────────────────────
        long? remoteSize = null;
        try
        {
            var head = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, activeUrl));
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
            var response = await _http.GetAsync(activeUrl, HttpCompletionOption.ResponseHeadersRead);
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

        // ── Shader deployment ─────────────────────────────────────────────────────
        // DC installation: remove the local reshade-shaders folder (ReShade will use
        // the DC global Reshade path instead). If the folder was user-owned, it gets
        // renamed to reshade-shaders-original and is NOT deleted.
        var effectiveShaderMode = ResolveShaderMode(shaderModeOverride);
        // DC always takes over shader management — remove local game folder
        ShaderPackService.RemoveFromGameFolder(installPath);

        // Sync shaders to the DC global folder (prune + deploy for mode changes).
        // Off mode triggers removal inside SyncDcFolder.
        if (dcMode)
            ShaderPackService.SyncDcFolder(effectiveShaderMode);

        var record = new AuxInstalledRecord
        {
            GameName       = gameName,
            InstallPath    = installPath,
            AddonType      = TypeDc,
            InstalledAs    = destName,
            SourceUrl      = activeUrl,
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
        bool dcIsInstalled = false,
        string? shaderModeOverride = null,
        bool use32Bit = false,
        IProgress<(string message, double percent)>? progress = null)
    {
        Directory.CreateDirectory(DownloadCacheDir);

        // 32-bit mode: install as dxgi.dll (same name) but from the 32-bit DLL.
        // DC Mode ON + 32-bit: install as ReShade32.dll.
        // DC Mode ON + 64-bit: install as ReShade64.dll.
        var destName = dcMode
            ? (use32Bit ? RsDcModeName32 : RsDcModeName)
            : RsNormalName;
        var destPath = Path.Combine(installPath, destName);

        // Remove the opposing-mode ReShade file if DC Mode is OFF
        // (when OFF, old DC-mode ReShade64.dll/ReShade32.dll can be safely replaced).
        // Never touch dxgi.dll here — it may belong to DC.
        if (!dcMode)
        {
            var rsOldPath64 = Path.Combine(installPath, RsDcModeName);
            if (File.Exists(rsOldPath64)) try { File.Delete(rsOldPath64); } catch { }
            var rsOldPath32 = Path.Combine(installPath, RsDcModeName32);
            if (File.Exists(rsOldPath32)) try { File.Delete(rsOldPath32); } catch { }
        }

        // ── Ensure staged DLLs exist (restore from app bundle if missing) ────────
        progress?.Report(("Preparing ReShade files...", 10));
        EnsureReShadeStaging();

        var rsStagedPath = use32Bit ? RsStagedPath32 : RsStagedPath64;
        var rsBundleName = use32Bit ? RsBundle32     : RsBundle64;
        if (!File.Exists(rsStagedPath))
            throw new FileNotFoundException(
                $"{rsBundleName} was not found in the app folder or staging directory.\n" +
                $"Expected: {rsStagedPath}\n" +
                $"Please ensure {rsBundleName} is placed next to RenoDXCommander.exe.");

        // ── Copy staged DLL to game folder ────────────────────────────────────────
        progress?.Report(("Installing ReShade...", 80));
        File.Copy(rsStagedPath, destPath, overwrite: true);

        // ── Deploy reshade.ini if not already present ─────────────────────────────
        // Seeds the game folder with the bundled reshade.ini so ReShade has sane
        // defaults on first launch. An existing user-modified ini is never overwritten.
        // The ini is intentionally left in place if ReShade is later uninstalled.
        try
        {
            var gameIniPath = Path.Combine(installPath, "reshade.ini");
            if (!File.Exists(gameIniPath) && File.Exists(RsIniPath))
            {
                File.Copy(RsIniPath, gameIniPath, overwrite: false);
                CrashReporter.Log($"InstallReShade: seeded reshade.ini to {installPath}");
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"InstallReShade: failed to seed reshade.ini — {ex.Message}");
        }

        progress?.Report(("ReShade installed!", 100));

        // ── Shader deployment ─────────────────────────────────────────────────────
        // DC Mode ON  → shaders go to DC global path.
        // DC Mode OFF + DC NOT installed → sync reshade-shaders folder to game dir.
        // DC Mode OFF + DC IS  installed → DC already handles shaders; skip.
        // Uses Sync (prune + deploy) so switching shader modes properly removes
        // files from the previous mode. Off mode triggers removal inside Sync.
        var effectiveShaderMode = ResolveShaderMode(shaderModeOverride);
        if (dcMode)
            ShaderPackService.SyncDcFolder(effectiveShaderMode);
        else if (!dcIsInstalled)
            ShaderPackService.SyncGameFolder(installPath, effectiveShaderMode);

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

        // If a user-owned reshade-shaders folder was renamed to reshade-shaders-original
        // when we deployed ours, restore it now that RS/DC has been uninstalled.
        if (!string.IsNullOrEmpty(record.InstallPath))
            ShaderPackService.RestoreOriginalIfPresent(record.InstallPath);
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

    /// <summary>
    /// Resolves the effective shader deploy mode from a per-game override string.
    /// null → use the global mode. "Off"/"Minimum"/"All"/"User" → override.
    /// </summary>
    private static ShaderPackService.DeployMode ResolveShaderMode(string? shaderModeOverride)
    {
        if (shaderModeOverride != null
            && Enum.TryParse<ShaderPackService.DeployMode>(shaderModeOverride, true, out var overrideMode))
            return overrideMode;
        return ShaderPackService.CurrentMode;
    }
}
