using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Bug condition exploration tests for DLL override name collision detection.
/// Generates random (rsName, dcName) pairs where rsName == dcName (case-insensitive)
/// and verifies the system rejects the collision via IsNameOccupiedByOtherComponent.
///
/// On UNFIXED code this should FAIL (collision allowed).
/// After the fix is applied, these tests should PASS.
///
/// **Validates: Requirements 2.3**
/// </summary>
public class DllOverrideCollisionExplorationTests : IDisposable
{
    private static readonly string[] DllNames = DllOverrideConstants.CommonDllNames;

    private readonly string _tempRoot;
    private readonly DllOverrideService _service;

    public DllOverrideCollisionExplorationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcCollision_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);

        var auxInstaller = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
        _service = new DllOverrideService(auxInstaller);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a DLL name from the common set, then produces a case-variant copy
    /// so rsName == dcName case-insensitively but may differ in casing.
    /// </summary>
    private static Gen<(string rsName, string dcName, bool is32Bit)> GenCollidingPair()
    {
        var genName = Gen.Elements(DllNames);
        var genCaseVariant = genName.Select(n =>
        {
            // Produce a case variant: uppercase first char
            return char.ToUpper(n[0]) + n[1..];
        });

        return from baseName in genName
               from variant in Gen.OneOf(
                   Gen.Constant(baseName),                          // exact match
                   Gen.Constant(baseName.ToUpperInvariant()),       // all upper
                   Gen.Constant(char.ToUpper(baseName[0]) + baseName[1..])) // first char upper
               from is32Bit in Arb.Default.Bool().Generator
               select (baseName, variant, is32Bit);
    }

    // ── Property: Collision detection rejects matching RS/DC names ─────────────────

    /// <summary>
    /// Property: For any RS/DC name pair where the names match (case-insensitive),
    /// attempting to set DC to the same name as RS SHALL be rejected.
    ///
    /// Sets up an RS override with rsName, then checks if the system detects
    /// that dcName (which matches rsName case-insensitively) is occupied.
    ///
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property BugCondition_CollidingNames_ShallBeRejected()
    {
        return Prop.ForAll(
            Arb.From(GenCollidingPair()),
            (tuple) =>
            {
                var (rsName, dcName, is32Bit) = tuple;

                // Arrange: create a game folder and set RS override
                var gameName = "CollisionTest_" + Guid.NewGuid().ToString("N")[..6];
                var gameFolder = Path.Combine(_tempRoot, gameName);
                Directory.CreateDirectory(gameFolder);

                // Place an RS file on disk with the rsName
                File.WriteAllBytes(Path.Combine(gameFolder, rsName), new byte[] { 0x00 });

                // Set the RS override in the service so GetEffectiveRsName returns rsName
                _service.SetDllOverride(gameName, rsName, "");

                // Act: check if DC can use dcName (which matches rsName case-insensitively)
                var isOccupied = _service.IsNameOccupiedByOtherComponent(
                    gameName, dcName, "DC", is32Bit, gameFolder);

                // Assert: the collision should be detected (isOccupied == true)
                return isOccupied
                    .Label($"IsNameOccupiedByOtherComponent should return true for " +
                           $"rsName='{rsName}', dcName='{dcName}', is32Bit={is32Bit} " +
                           $"but returned {isOccupied}");
            });
    }
}
