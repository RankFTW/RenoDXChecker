using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for update check staging DLL selection.
/// Feature: reshade-no-addon-support, Property 8: Update check uses correct staging DLLs
/// **Validates: Requirements 9.2**
/// </summary>
[Collection("StagingFiles")]
public class NormalReShadeUpdateCheckStagingPropertyTests : IDisposable
{
    // ── Staging directories ──
    private static readonly string AddonStagingDir  = AuxInstallService.RsStagingDir;
    private static readonly string NormalStagingDir  = AuxInstallService.RsNormalStagingDir;

    /// <summary>Backup of original staging files so we can restore after tests.</summary>
    private readonly Dictionary<string, byte[]?> _originalFiles = new();
    private readonly List<string> _tempGameDirs = new();

    public NormalReShadeUpdateCheckStagingPropertyTests()
    {
        // Back up existing staging files before seeding test content
        BackupStagingFile(AuxInstallService.RsStagedPath64);
        BackupStagingFile(AuxInstallService.RsStagedPath32);
        BackupStagingFile(AuxInstallService.RsNormalStagedPath64);
        BackupStagingFile(AuxInstallService.RsNormalStagedPath32);
    }

    public void Dispose()
    {
        // Restore original staging files with retry logic
        foreach (var (path, original) in _originalFiles)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (original != null)
                        WriteWithRetry(path, original);
                    else if (File.Exists(path))
                        File.Delete(path);
                    break; // success
                }
                catch
                {
                    if (attempt < 4) Thread.Sleep(100 * (attempt + 1));
                    /* best effort */
                }
            }

            // Verify restoration
            try
            {
                if (original != null)
                {
                    if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(original))
                        WriteWithRetry(path, original);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { /* best effort */ }
        }

        // Clean up temp game directories
        foreach (var dir in _tempGameDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private void BackupStagingFile(string path)
    {
        _originalFiles[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private void SeedStagingFile(string path, int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteWithRetry(path, new byte[size]);
    }

    private static void WriteWithRetry(string path, byte[] content, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try { File.WriteAllBytes(path, content); return; }
            catch (IOException) when (i < maxRetries - 1) { Thread.Sleep(50 * (i + 1)); }
        }
    }

    private string CreateTempGameDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"RdxcTest_UpdChk_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempGameDirs.Add(dir);
        return dir;
    }

    // ── Generators ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates distinct staging sizes for Addon and Normal variants (64 and 32 bit),
    /// plus an installed file size that may or may not match the correct staging size.
    /// </summary>
    private static readonly Gen<(int addon64Size, int addon32Size, int normal64Size, int normal32Size, bool useNormal, bool matchesStaging)> GenScenario =
        from addon64  in Gen.Choose(100, 5000)
        from addon32  in Gen.Choose(100, 5000)
        from normal64 in Gen.Choose(100, 5000)
        from normal32 in Gen.Choose(100, 5000)
        from useNormal in Gen.Elements(true, false)
        from matchesStaging in Gen.Elements(true, false)
        // Ensure addon and normal sizes are distinct so cross-contamination is detectable.
        // All four sizes must be distinct to prevent accidental cross-matches
        // (e.g. addon32 matching normal64 would confuse the independence test).
        where addon64 != normal64 && addon32 != normal32
              && addon64 != addon32 && normal64 != normal32
              && addon64 != normal32 && addon32 != normal64
        select (addon64, addon32, normal64, normal32, useNormal, matchesStaging);

    // ── Property 8: Update check uses correct staging DLLs ──────────────────────
    // Feature: reshade-no-addon-support, Property 8: Update check uses correct staging DLLs
    // **Validates: Requirements 9.2**

    [Property(MaxTest = 100)]
    public Property CheckReShadeUpdateLocal_UsesCorrectStagingDlls()
    {
        return Prop.ForAll(GenScenario.ToArbitrary(), scenario =>
        {
            var (addon64Size, addon32Size, normal64Size, normal32Size, useNormal, matchesStaging) = scenario;

            // Seed all four staging DLLs with distinct sizes
            SeedStagingFile(AuxInstallService.RsStagedPath64, addon64Size);
            SeedStagingFile(AuxInstallService.RsStagedPath32, addon32Size);
            SeedStagingFile(AuxInstallService.RsNormalStagedPath64, normal64Size);
            SeedStagingFile(AuxInstallService.RsNormalStagedPath32, normal32Size);

            // Create a temp game dir with an installed DLL
            var gameDir = CreateTempGameDir();
            var dllName = "dxgi.dll";
            var installedPath = Path.Combine(gameDir, dllName);

            // Pick the correct staging size for this variant (use 64-bit as the reference)
            var correctStagingSize = useNormal ? normal64Size : addon64Size;

            // If matchesStaging, installed file matches the correct staging size → no update.
            // If !matchesStaging, installed file has a different size → update available.
            var installedSize = matchesStaging
                ? correctStagingSize
                : correctStagingSize + 1; // guaranteed different

            File.WriteAllBytes(installedPath, new byte[installedSize]);

            var record = new AuxInstalledRecord
            {
                GameName    = "TestGame",
                InstallPath = gameDir,
                AddonType   = useNormal ? AuxInstallService.TypeReShadeNormal : AuxInstallService.TypeReShade,
                InstalledAs = dllName,
            };

            var updateAvailable = AuxInstallService.CheckReShadeUpdateLocal(record);

            // When installed size matches the correct staging DLL → no update (false)
            // When installed size differs → update available (true)
            var expectedUpdate = !matchesStaging;

            return (updateAvailable == expectedUpdate)
                .Label($"Expected updateAvailable={expectedUpdate} but got {updateAvailable} " +
                       $"(useNormal={useNormal}, matchesStaging={matchesStaging}, " +
                       $"installedSize={installedSize}, correctStagingSize={correctStagingSize})");
        });
    }

    /// <summary>
    /// The two staging directories are independent — a size match in one
    /// should not affect the result for the other.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CheckReShadeUpdateLocal_StagingDirectoriesAreIndependent()
    {
        return Prop.ForAll(GenScenario.ToArbitrary(), scenario =>
        {
            var (addon64Size, addon32Size, normal64Size, normal32Size, useNormal, _) = scenario;

            // Seed all four staging DLLs with distinct sizes
            SeedStagingFile(AuxInstallService.RsStagedPath64, addon64Size);
            SeedStagingFile(AuxInstallService.RsStagedPath32, addon32Size);
            SeedStagingFile(AuxInstallService.RsNormalStagedPath64, normal64Size);
            SeedStagingFile(AuxInstallService.RsNormalStagedPath32, normal32Size);

            var gameDir = CreateTempGameDir();
            var dllName = "dxgi.dll";
            var installedPath = Path.Combine(gameDir, dllName);

            // Install a DLL that matches the WRONG staging directory's size
            // (matches the other variant, not the one indicated by AddonType)
            var wrongStagingSize = useNormal ? addon64Size : normal64Size;
            File.WriteAllBytes(installedPath, new byte[wrongStagingSize]);

            var record = new AuxInstalledRecord
            {
                GameName    = "TestGame",
                InstallPath = gameDir,
                AddonType   = useNormal ? AuxInstallService.TypeReShadeNormal : AuxInstallService.TypeReShade,
                InstalledAs = dllName,
            };

            var updateAvailable = AuxInstallService.CheckReShadeUpdateLocal(record);

            // Since the installed file matches the WRONG staging dir (not the one for this AddonType),
            // the check should report an update is available (true).
            return updateAvailable
                .Label($"Expected update=true because installed matches wrong staging dir " +
                       $"(useNormal={useNormal}, wrongStagingSize={wrongStagingSize}, " +
                       $"addon64={addon64Size}, normal64={normal64Size})");
        });
    }
}
