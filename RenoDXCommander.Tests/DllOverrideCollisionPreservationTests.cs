using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Preservation tests for DLL override operations with non-colliding RS/DC name pairs.
/// Verifies that EnableDllOverride and UpdateDllOverrideNames produce the expected results
/// (file renamed, config persisted) for valid inputs that do NOT trigger the bug condition.
///
/// **Validates: Requirements 3.1, 3.2, 3.5, 3.7**
/// </summary>
public class DllOverrideCollisionPreservationTests : IDisposable
{
    private static readonly string[] DllNames = DllOverrideConstants.CommonDllNames;

    private readonly string _tempRoot;
    private readonly AuxInstallService _auxService;
    private readonly DllOverrideService _service;
    private readonly List<AuxInstalledRecord> _seededRecords = new();

    public DllOverrideCollisionPreservationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcPres_" + Guid.NewGuid().ToString("N")[..8]);
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

    /// <summary>
    /// Generates a pair of distinct DLL names from CommonDllNames (non-colliding,
    /// case-insensitive) plus a bitness flag.
    /// </summary>
    private static Gen<(string rsName, string dcName, bool is32Bit)> GenNonCollidingPair()
    {
        return from rsIdx in Gen.Choose(0, DllNames.Length - 1)
               from dcIdx in Gen.Choose(0, DllNames.Length - 1)
                              .Where(i => !DllNames[i].Equals(DllNames[rsIdx], StringComparison.OrdinalIgnoreCase))
               from is32Bit in Arb.Default.Bool().Generator
               select (DllNames[rsIdx], DllNames[dcIdx], is32Bit);
    }

    // ── Property: EnableDllOverride renames files and persists config ──────────────

    /// <summary>
    /// Property: For any valid non-colliding RS/DC name pair, EnableDllOverride SHALL
    /// rename the RS file on disk to the new name and persist the override config.
    ///
    /// **Validates: Requirements 3.1, 3.2, 3.5, 3.7**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property Preservation_EnableDllOverride_RenamesAndPersists()
    {
        return Prop.ForAll(
            Arb.From(GenNonCollidingPair()),
            (tuple) =>
            {
                var (rsName, dcName, is32Bit) = tuple;

                // Arrange
                var gameName = "EnablePres_" + Guid.NewGuid().ToString("N")[..6];
                var gameFolder = Path.Combine(_tempRoot, gameName);
                Directory.CreateDirectory(gameFolder);

                // Place an RS file under the default name
                var defaultRsPath = Path.Combine(gameFolder, AuxInstallService.RsNormalName);
                var rsContent = new byte[] { 0xAA, 0xBB, 0xCC };
                File.WriteAllBytes(defaultRsPath, rsContent);

                // Create RS record
                var rsRecord = new AuxInstalledRecord
                {
                    GameName = gameName,
                    InstallPath = gameFolder,
                    AddonType = "ReShade",
                    InstalledAs = AuxInstallService.RsNormalName,
                    InstalledAt = DateTime.UtcNow,
                };
                _auxService.SaveAuxRecord(rsRecord);
                _seededRecords.Add(rsRecord);

                var card = new GameCardViewModel
                {
                    GameName = gameName,
                    InstallPath = gameFolder,
                    Is32Bit = is32Bit,
                    RsInstalledFile = AuxInstallService.RsNormalName,
                    DllOverrideEnabled = false,
                };
                card.RsRecord = rsRecord;

                // Act: enable override with non-colliding names
                _service.EnableDllOverride(card, rsName, dcName);

                // Assert
                // 1. RS file should now exist under the new name
                var newRsPath = Path.Combine(gameFolder, rsName);
                var rsFileRenamed = File.Exists(newRsPath);

                // 2. Old RS file should be gone (unless rsName == default, which won't happen
                //    since we pick from CommonDllNames and default is dxgi.dll which is in the list,
                //    but the pair is non-colliding so at most one can be dxgi.dll)
                var oldRsGone = rsName.Equals(AuxInstallService.RsNormalName, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(defaultRsPath);

                // 3. Config should be persisted
                var cfg = _service.GetDllOverride(gameName);
                var configPersisted = cfg != null
                    && cfg.ReShadeFileName.Equals(rsName, StringComparison.OrdinalIgnoreCase)
                    && cfg.DcFileName.Equals(dcName, StringComparison.OrdinalIgnoreCase);

                // 4. Card state should be updated
                var cardUpdated = card.DllOverrideEnabled
                    && card.RsInstalledFile != null
                    && card.RsInstalledFile.Equals(rsName, StringComparison.OrdinalIgnoreCase);

                return (rsFileRenamed && oldRsGone && configPersisted && cardUpdated)
                    .Label($"EnableDllOverride(rs='{rsName}', dc='{dcName}', 32bit={is32Bit}): " +
                           $"rsRenamed={rsFileRenamed}, oldGone={oldRsGone}, " +
                           $"configPersisted={configPersisted}, cardUpdated={cardUpdated}");
            });
    }

    // ── Property: UpdateDllOverrideNames renames files and persists config ─────────

    /// <summary>
    /// Property: For any valid non-colliding RS/DC name pair, UpdateDllOverrideNames SHALL
    /// rename the RS file from the old override name to the new name and update the config.
    ///
    /// **Validates: Requirements 3.1, 3.7**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property Preservation_UpdateDllOverrideNames_RenamesAndPersists()
    {
        return Prop.ForAll(
            Arb.From(GenNonCollidingPair()),
            (tuple) =>
            {
                var (newRsName, newDcName, is32Bit) = tuple;

                // Arrange: start with an existing override using a known initial name
                var gameName = "UpdatePres_" + Guid.NewGuid().ToString("N")[..6];
                var gameFolder = Path.Combine(_tempRoot, gameName);
                Directory.CreateDirectory(gameFolder);

                // Pick an initial RS name that differs from the new one
                var initialRsName = newRsName.Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase)
                    ? "dinput8.dll"
                    : "d3d11.dll";

                // Place RS file under the initial override name
                var initialRsPath = Path.Combine(gameFolder, initialRsName);
                var rsContent = new byte[] { 0xDD, 0xEE, 0xFF };
                File.WriteAllBytes(initialRsPath, rsContent);

                // Create RS record
                var rsRecord = new AuxInstalledRecord
                {
                    GameName = gameName,
                    InstallPath = gameFolder,
                    AddonType = "ReShade",
                    InstalledAs = initialRsName,
                    InstalledAt = DateTime.UtcNow,
                };
                _auxService.SaveAuxRecord(rsRecord);
                _seededRecords.Add(rsRecord);

                // Set initial override config
                _service.SetDllOverride(gameName, initialRsName, "");

                var card = new GameCardViewModel
                {
                    GameName = gameName,
                    InstallPath = gameFolder,
                    Is32Bit = is32Bit,
                    RsInstalledFile = initialRsName,
                    DllOverrideEnabled = true,
                };
                card.RsRecord = rsRecord;

                // Act: update override names
                _service.UpdateDllOverrideNames(card, newRsName, newDcName);

                // Assert
                // 1. RS file should now exist under the new name
                var newRsPath = Path.Combine(gameFolder, newRsName);
                var rsFileRenamed = File.Exists(newRsPath);

                // 2. Old RS file should be gone
                var oldRsGone = newRsName.Equals(initialRsName, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(initialRsPath);

                // 3. Config should reflect the new names
                var cfg = _service.GetDllOverride(gameName);
                var configUpdated = cfg != null
                    && cfg.ReShadeFileName.Equals(newRsName, StringComparison.OrdinalIgnoreCase)
                    && cfg.DcFileName.Equals(newDcName, StringComparison.OrdinalIgnoreCase);

                return (rsFileRenamed && oldRsGone && configUpdated)
                    .Label($"UpdateDllOverrideNames(newRs='{newRsName}', newDc='{newDcName}', 32bit={is32Bit}): " +
                           $"rsRenamed={rsFileRenamed}, oldGone={oldRsGone}, configUpdated={configUpdated}");
            });
    }
}
