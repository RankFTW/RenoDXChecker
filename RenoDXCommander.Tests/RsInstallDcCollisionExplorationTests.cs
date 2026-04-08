using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Bug condition exploration tests for RS install when DC occupies the default target name.
/// Generates scenarios where DC is installed as dxgi.dll and RS is installed without an
/// override, verifying RS falls back to ReShade64.dll/ReShade32.dll instead of overwriting DC.
///
/// On UNFIXED code this should FAIL (DC overwritten).
/// After the fix is applied, these tests should PASS.
///
/// **Validates: Requirements 2.4**
/// </summary>
public class RsInstallDcCollisionExplorationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AuxInstallService _service;
    private readonly List<AuxInstalledRecord> _seededRecords = new();

    public RsInstallDcCollisionExplorationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcRsDc_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
        _service = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
    }

    public void Dispose()
    {
        foreach (var record in _seededRecords)
        {
            try { _service.RemoveRecord(record); } catch { }
        }
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // ── Generators ────────────────────────────────────────────────────────────────

    private static Gen<bool> GenBitness() => Arb.Default.Bool().Generator;

    // ── Property: RS install falls back when DC occupies default target ────────────

    /// <summary>
    /// Property: For any RS install where DC occupies the default RS target name (dxgi.dll),
    /// RS SHALL fall back to ReShade64.dll or ReShade32.dll instead of overwriting DC.
    ///
    /// Sets up a DC record with InstalledAs = "dxgi.dll", places a DC file at that path,
    /// then calls InstallReShadeAsync without a filenameOverride. Verifies:
    /// 1. The DC file at dxgi.dll is NOT overwritten (content preserved)
    /// 2. RS is installed as ReShade64.dll or ReShade32.dll (based on bitness)
    /// 3. The returned record reflects the fallback name
    ///
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public Property BugCondition_RsInstall_ShallFallbackWhenDcOccupiesTarget()
    {
        return Prop.ForAll(
            Arb.From(GenBitness()),
            (bool use32Bit) =>
            {
                // Arrange
                var gameName = "RsDcCollision_" + Guid.NewGuid().ToString("N")[..6];
                var gameFolder = Path.Combine(_tempRoot, gameName);
                Directory.CreateDirectory(gameFolder);

                // Place a DC file at dxgi.dll (the default RS target)
                var dcFilePath = Path.Combine(gameFolder, AuxInstallService.RsNormalName);
                var dcContent = new byte[] { 0xDC, 0x44, 0x43, 0x01 };
                File.WriteAllBytes(dcFilePath, dcContent);

                // Seed a DC record so FindRecord("DisplayCommander") returns it
                var dcRecord = new AuxInstalledRecord
                {
                    GameName = gameName,
                    InstallPath = gameFolder,
                    AddonType = "DisplayCommander",
                    InstalledAs = AuxInstallService.RsNormalName, // DC is at dxgi.dll
                    InstalledAt = DateTime.UtcNow,
                };
                _service.SaveAuxRecord(dcRecord);
                _seededRecords.Add(dcRecord);

                // Ensure staged ReShade DLLs exist
                EnsureStagedReShadeExists(use32Bit);

                // Act: install RS without a filenameOverride
                var rsRecord = _service.InstallReShadeAsync(
                    gameName,
                    gameFolder,
                    use32Bit: use32Bit).GetAwaiter().GetResult();
                _seededRecords.Add(rsRecord);

                // Assert
                var expectedFallback = use32Bit ? AuxInstallService.RsStaged32 : AuxInstallService.RsStaged64;

                // 1. DC file at dxgi.dll should still have its original content
                var dcFileExists = File.Exists(dcFilePath);
                var dcContentIntact = dcFileExists && File.ReadAllBytes(dcFilePath).SequenceEqual(dcContent);

                // 2. RS record should reflect the fallback name
                var rsUsedFallback = string.Equals(rsRecord.InstalledAs, expectedFallback,
                    StringComparison.OrdinalIgnoreCase);

                // 3. RS fallback file should exist on disk
                var rsFallbackPath = Path.Combine(gameFolder, expectedFallback);
                var rsFallbackExists = File.Exists(rsFallbackPath);

                return (dcContentIntact && rsUsedFallback && rsFallbackExists)
                    .Label($"RS install with DC at dxgi.dll (use32Bit={use32Bit}): " +
                           $"dcContentIntact={dcContentIntact}, " +
                           $"rsUsedFallback={rsUsedFallback} (InstalledAs='{rsRecord.InstalledAs}', expected='{expectedFallback}'), " +
                           $"rsFallbackExists={rsFallbackExists}");
            });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates fake DLL bytes that pass IsReShadeFileStrict's binary signature scan.
    /// </summary>
    private static byte[] CreateFakeReShadeBytes()
    {
        var content = new byte[1024];
        var rng = new System.Random(42);
        rng.NextBytes(content);
        var markers = "ReShade version 5.9.2 | https://reshade.me | by crosire"u8;
        markers.CopyTo(content.AsSpan(256));
        return content;
    }

    /// <summary>
    /// Ensures the ReShade staging directory has the appropriate DLL for the given bitness.
    /// </summary>
    private static void EnsureStagedReShadeExists(bool use32Bit)
    {
        Directory.CreateDirectory(AuxInstallService.RsStagingDir);
        var path = use32Bit ? AuxInstallService.RsStagedPath32 : AuxInstallService.RsStagedPath64;
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, CreateFakeReShadeBytes());
        }
        // Also ensure the other bitness exists (InstallReShadeAsync may need it)
        var otherPath = use32Bit ? AuxInstallService.RsStagedPath64 : AuxInstallService.RsStagedPath32;
        if (!File.Exists(otherPath))
        {
            File.WriteAllBytes(otherPath, CreateFakeReShadeBytes());
        }
    }
}
