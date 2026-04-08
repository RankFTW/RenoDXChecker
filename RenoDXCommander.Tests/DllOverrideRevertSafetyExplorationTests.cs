using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Bug condition exploration tests for DLL override toggle-off revert safety.
/// Generates random override states where the default name is occupied by another file,
/// toggles override OFF, and verifies the occupied file is not overwritten.
///
/// On UNFIXED code this should FAIL (file overwritten).
/// After the fix is applied, these tests should PASS.
///
/// **Validates: Requirements 2.5, 2.6**
/// </summary>
public class DllOverrideRevertSafetyExplorationTests : IDisposable
{
    /// <summary>Custom RS override names that differ from the default dxgi.dll.</summary>
    private static readonly string[] CustomRsNames = { "d3d11.dll", "dinput8.dll", "version.dll", "winmm.dll" };

    /// <summary>Custom DC override names that differ from the default addon name.</summary>
    private static readonly string[] CustomDcNames = { "dxgi.dll", "d3d9.dll", "opengl32.dll" };

    private readonly string _tempRoot;
    private readonly AuxInstallService _auxService;
    private readonly DllOverrideService _service;
    private readonly List<AuxInstalledRecord> _seededRecords = new();

    public DllOverrideRevertSafetyExplorationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcRevert_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);

        _auxService = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
        _service = new DllOverrideService(_auxService);
    }

    public void Dispose()
    {
        foreach (var record in _seededRecords)
        {
            try { _auxService.RemoveRecord(record); } catch { }
        }
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // ── Generators ────────────────────────────────────────────────────────────────

    private static Gen<(string customRsName, bool is32Bit)> GenRsRevertScenario()
    {
        return from rsName in Gen.Elements(CustomRsNames)
               from is32Bit in Arb.Default.Bool().Generator
               select (rsName, is32Bit);
    }

    private static Gen<(string customDcName, bool is32Bit)> GenDcRevertScenario()
    {
        return from dcName in Gen.Elements(CustomDcNames)
               from is32Bit in Arb.Default.Bool().Generator
               select (dcName, is32Bit);
    }

    // ── Property: RS toggle-off does not overwrite occupied default name ───────────

    /// <summary>
    /// Property: For any toggle-off operation where the default RS filename (dxgi.dll)
    /// is occupied by another file, the occupied file SHALL NOT be deleted or overwritten.
    ///
    /// Sets up RS with a custom override name, places a foreign file at dxgi.dll,
    /// then disables the override and checks the foreign file survives.
    ///
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 4)]
    public Property BugCondition_RsRevert_ShallNotOverwriteOccupiedDefault()
    {
        return Prop.ForAll(
            Arb.From(GenRsRevertScenario()),
            (tuple) =>
            {
                var (customRsName, is32Bit) = tuple;

                // Arrange
                var gameName = "RsRevert_" + Guid.NewGuid().ToString("N")[..6];
                var gameFolder = Path.Combine(_tempRoot, gameName);
                Directory.CreateDirectory(gameFolder);

                // Place the RS file under its custom override name
                var rsFilePath = Path.Combine(gameFolder, customRsName);
                var rsContent = new byte[] { 0xAA, 0x01, 0x02, 0x03 };
                File.WriteAllBytes(rsFilePath, rsContent);

                // Place a foreign file at the default name (dxgi.dll) — this is the "occupied" file
                var defaultRsPath = Path.Combine(gameFolder, AuxInstallService.RsNormalName);
                var foreignContent = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };
                File.WriteAllBytes(defaultRsPath, foreignContent);

                // Create RS record and card
                var rsRecord = new AuxInstalledRecord
                {
                    GameName = gameName,
                    InstallPath = gameFolder,
                    AddonType = "ReShade",
                    InstalledAs = customRsName,
                    InstalledAt = DateTime.UtcNow,
                };
                _auxService.SaveAuxRecord(rsRecord);
                _seededRecords.Add(rsRecord);

                var card = new GameCardViewModel
                {
                    GameName = gameName,
                    InstallPath = gameFolder,
                    Is32Bit = is32Bit,
                    RsInstalledFile = customRsName,
                    DllOverrideEnabled = true,
                };
                card.RsRecord = rsRecord;

                // Set the override config
                _service.SetDllOverride(gameName, customRsName, "");

                // Act: disable the override (toggle OFF)
                var result = _service.DisableDllOverride(card);

                // Assert: the foreign file at dxgi.dll should still have its original content
                var foreignFileExists = File.Exists(defaultRsPath);
                var foreignFileIntact = foreignFileExists && File.ReadAllBytes(defaultRsPath).SequenceEqual(foreignContent);

                return foreignFileIntact
                    .Label($"Foreign file at '{AuxInstallService.RsNormalName}' should be intact after RS revert " +
                           $"(customRsName='{customRsName}', is32Bit={is32Bit}): " +
                           $"exists={foreignFileExists}, contentMatch={foreignFileIntact}, rsReverted={result.RsReverted}");
            });
    }

    // ── Property: DC toggle-off does not overwrite occupied default name ───────────

    /// <summary>
    /// Property: For any toggle-off operation where the default DC filename
    /// (zzz_display_commander_lite.addon64/32) is occupied by another file,
    /// the occupied file SHALL NOT be deleted or overwritten.
    ///
    /// **Validates: Requirements 2.6**
    /// </summary>
    [Property(MaxTest = 4)]
    public Property BugCondition_DcRevert_ShallNotOverwriteOccupiedDefault()
    {
        return Prop.ForAll(
            Arb.From(GenDcRevertScenario()),
            (tuple) =>
            {
                var (customDcName, is32Bit) = tuple;

                // Arrange
                var gameName = "DcRevert_" + Guid.NewGuid().ToString("N")[..6];
                var gameFolder = Path.Combine(_tempRoot, gameName);
                Directory.CreateDirectory(gameFolder);

                var defaultDcName = is32Bit
                    ? "zzz_display_commander_lite.addon32"
                    : "zzz_display_commander_lite.addon64";

                // For DC, the deploy path is the game folder itself (no reshade.ini → no AddonPath)
                // Place the DC file under its custom override name
                var dcFilePath = Path.Combine(gameFolder, customDcName);
                var dcContent = new byte[] { 0xDC, 0x01, 0x02, 0x03 };
                File.WriteAllBytes(dcFilePath, dcContent);

                // Place a foreign file at the default DC name — this is the "occupied" file
                var defaultDcPath = Path.Combine(gameFolder, defaultDcName);
                var foreignContent = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };
                File.WriteAllBytes(defaultDcPath, foreignContent);

                // Create DC record
                var dcRecord = new AuxInstalledRecord
                {
                    GameName = gameName,
                    InstallPath = gameFolder,
                    AddonType = "DisplayCommander",
                    InstalledAs = customDcName,
                    InstalledAt = DateTime.UtcNow,
                };
                _auxService.SaveAuxRecord(dcRecord);
                _seededRecords.Add(dcRecord);

                var card = new GameCardViewModel
                {
                    GameName = gameName,
                    InstallPath = gameFolder,
                    Is32Bit = is32Bit,
                    DcInstalledFile = customDcName,
                    DllOverrideEnabled = true,
                };

                // Set the override config (RS name doesn't matter here, use default)
                _service.SetDllOverride(gameName, AuxInstallService.RsNormalName, customDcName);

                // Act: disable the override (toggle OFF)
                var result = _service.DisableDllOverride(card);

                // Assert: the foreign file at the default DC name should still have its original content
                var foreignFileExists = File.Exists(defaultDcPath);
                var foreignFileIntact = foreignFileExists && File.ReadAllBytes(defaultDcPath).SequenceEqual(foreignContent);

                return foreignFileIntact
                    .Label($"Foreign file at '{defaultDcName}' should be intact after DC revert " +
                           $"(customDcName='{customDcName}', is32Bit={is32Bit}): " +
                           $"exists={foreignFileExists}, contentMatch={foreignFileIntact}, dcReverted={result.DcReverted}");
            });
    }
}
