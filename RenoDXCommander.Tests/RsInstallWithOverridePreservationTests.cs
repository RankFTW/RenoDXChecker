using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Preservation tests for RS install with explicit filenameOverride.
/// Generates random filenameOverride values (non-null, non-colliding with DC) and verifies
/// InstallReShadeAsync uses the override name identically to the original code.
///
/// **Validates: Requirements 3.8**
/// </summary>
public class RsInstallWithOverridePreservationTests : IDisposable
{
    /// <summary>
    /// Override names that are valid DLL filenames and do NOT collide with the default
    /// DC addon name (zzz_display_commander_lite.addon64/32).
    /// </summary>
    private static readonly string[] OverrideNames =
    {
        "d3d11.dll",
        "d3d9.dll",
        "d3d10.dll",
        "d3d12.dll",
        "opengl32.dll",
        "dinput8.dll",
        "version.dll",
        "winmm.dll",
        "ddraw.dll",
        "ReShade64.dll",
        "ReShade32.dll",
    };

    private readonly string _tempRoot;
    private readonly AuxInstallService _service;
    private readonly List<AuxInstalledRecord> _seededRecords = new();

    public RsInstallWithOverridePreservationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcRsOvr_" + Guid.NewGuid().ToString("N")[..8]);
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

    /// <summary>
    /// Generates a filenameOverride from the override names list plus a bitness flag.
    /// These names do not collide with DC's default addon name.
    /// </summary>
    private static Gen<(string overrideName, bool use32Bit)> GenOverrideScenario()
    {
        return from name in Gen.Elements(OverrideNames)
               from is32Bit in Arb.Default.Bool().Generator
               select (name, is32Bit);
    }

    // ── Property: RS install with explicit override uses that name ─────────────────

    /// <summary>
    /// Property: For any RS install with an explicit filenameOverride that doesn't
    /// collide with DC, the install SHALL use that override name and produce a record
    /// with InstalledAs matching the override.
    ///
    /// **Validates: Requirements 3.8**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property Preservation_RsInstallWithOverride_UsesOverrideName()
    {
        return Prop.ForAll(
            Arb.From(GenOverrideScenario()),
            (tuple) =>
            {
                var (overrideName, use32Bit) = tuple;

                // Arrange
                var gameName = "RsOvrPres_" + Guid.NewGuid().ToString("N")[..6];
                var gameFolder = Path.Combine(_tempRoot, gameName);
                Directory.CreateDirectory(gameFolder);

                // Ensure staged ReShade DLLs exist
                EnsureStagedReShadeExists(use32Bit);

                // Act: install RS with an explicit filenameOverride
                var record = _service.InstallReShadeAsync(
                    gameName,
                    gameFolder,
                    use32Bit: use32Bit,
                    filenameOverride: overrideName).GetAwaiter().GetResult();
                _seededRecords.Add(record);

                // Assert
                // 1. Record should reflect the override name
                var recordUsesOverride = string.Equals(
                    record.InstalledAs, overrideName, StringComparison.OrdinalIgnoreCase);

                // 2. File should exist on disk under the override name
                var filePath = Path.Combine(gameFolder, overrideName);
                var fileExists = File.Exists(filePath);

                // 3. No file should exist under the default name (unless override IS the default)
                var defaultPath = Path.Combine(gameFolder, AuxInstallService.RsNormalName);
                var noDefaultFile = overrideName.Equals(AuxInstallService.RsNormalName, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(defaultPath);

                return (recordUsesOverride && fileExists && noDefaultFile)
                    .Label($"InstallReShadeAsync(override='{overrideName}', 32bit={use32Bit}): " +
                           $"recordUsesOverride={recordUsesOverride} (InstalledAs='{record.InstalledAs}'), " +
                           $"fileExists={fileExists}, noDefaultFile={noDefaultFile}");
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
        var otherPath = use32Bit ? AuxInstallService.RsStagedPath64 : AuxInstallService.RsStagedPath32;
        if (!File.Exists(otherPath))
        {
            File.WriteAllBytes(otherPath, CreateFakeReShadeBytes());
        }
    }
}
