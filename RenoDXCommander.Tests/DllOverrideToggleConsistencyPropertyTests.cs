using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Preservation tests for DLL override toggle consistency.
/// Generates random toggle sequences (ON/OFF with various name selections) and verifies
/// that the DllOverrideEnabled flag on the card always matches whether a DllOverrideConfig
/// exists for the game.
///
/// **Validates: Requirements 2.8, 3.1**
/// </summary>
public class DllOverrideToggleConsistencyPropertyTests : IDisposable
{
    private static readonly string[] DllNames = DllOverrideConstants.CommonDllNames;

    private readonly string _tempRoot;
    private readonly AuxInstallService _auxService;
    private readonly DllOverrideService _service;
    private readonly List<AuxInstalledRecord> _seededRecords = new();

    public DllOverrideToggleConsistencyPropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcToggle_" + Guid.NewGuid().ToString("N")[..8]);
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

    // ── Toggle operation model ────────────────────────────────────────────────────

    /// <summary>Represents a single toggle operation in a sequence.</summary>
    private record ToggleOp(bool TurnOn, string RsName, string DcName);

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a single toggle operation: ON with a non-colliding RS/DC name pair,
    /// or OFF (names ignored when turning off).
    /// </summary>
    private static Gen<ToggleOp> GenToggleOp()
    {
        var genOn = from rsIdx in Gen.Choose(0, DllNames.Length - 1)
                    from dcIdx in Gen.Choose(0, DllNames.Length - 1)
                                  .Where(i => !DllNames[i].Equals(DllNames[rsIdx], StringComparison.OrdinalIgnoreCase))
                    select new ToggleOp(true, DllNames[rsIdx], DllNames[dcIdx]);

        var genOff = Gen.Constant(new ToggleOp(false, "", ""));

        return Gen.OneOf(genOn, genOff);
    }

    /// <summary>
    /// Generates a sequence of 2–5 toggle operations.
    /// </summary>
    private static Gen<ToggleOp[]> GenToggleSequence()
    {
        return from count in Gen.Choose(2, 5)
               from ops in Gen.ArrayOf(count, GenToggleOp())
               select ops;
    }

    // ── Property: Toggle state always matches persisted config ─────────────────────

    /// <summary>
    /// Property: For any sequence of toggle operations, the DllOverrideEnabled flag
    /// on the card SHALL match whether a DllOverrideConfig exists for the game
    /// after each operation.
    ///
    /// **Validates: Requirements 2.8, 3.1**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property Preservation_ToggleState_MatchesPersistedConfig()
    {
        return Prop.ForAll(
            Arb.From(GenToggleSequence()),
            (ToggleOp[] ops) =>
            {
                // Arrange
                var gameName = "TogglePres_" + Guid.NewGuid().ToString("N")[..6];
                var gameFolder = Path.Combine(_tempRoot, gameName);
                Directory.CreateDirectory(gameFolder);

                // Place an RS file under the default name
                var defaultRsPath = Path.Combine(gameFolder, AuxInstallService.RsNormalName);
                File.WriteAllBytes(defaultRsPath, new byte[] { 0xAA, 0xBB });

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
                    Is32Bit = false,
                    RsInstalledFile = AuxInstallService.RsNormalName,
                    DllOverrideEnabled = false,
                };
                card.RsRecord = rsRecord;

                // Act & Assert: apply each toggle operation and check consistency
                for (int i = 0; i < ops.Length; i++)
                {
                    var op = ops[i];

                    if (op.TurnOn)
                    {
                        // Ensure the RS file exists at whatever name the record says
                        var currentRsPath = Path.Combine(gameFolder, card.RsRecord.InstalledAs);
                        if (!File.Exists(currentRsPath))
                            File.WriteAllBytes(currentRsPath, new byte[] { 0xAA, 0xBB });

                        _service.EnableDllOverride(card, op.RsName, op.DcName);
                    }
                    else
                    {
                        if (_service.HasDllOverride(gameName))
                        {
                            // Ensure the RS file exists at the override name before disabling
                            var cfg = _service.GetDllOverride(gameName);
                            if (cfg != null)
                            {
                                var overridePath = Path.Combine(gameFolder, cfg.ReShadeFileName);
                                if (!File.Exists(overridePath))
                                    File.WriteAllBytes(overridePath, new byte[] { 0xAA, 0xBB });
                            }
                            _service.DisableDllOverride(card);
                        }
                    }

                    // Check invariant: card.DllOverrideEnabled matches config existence
                    var hasConfig = _service.HasDllOverride(gameName);
                    if (card.DllOverrideEnabled != hasConfig)
                    {
                        return false.Label(
                            $"After op[{i}] (TurnOn={op.TurnOn}, rs='{op.RsName}', dc='{op.DcName}'): " +
                            $"card.DllOverrideEnabled={card.DllOverrideEnabled} but HasDllOverride={hasConfig}");
                    }
                }

                return true.Label("Toggle state matched persisted config after all operations");
            });
    }
}
