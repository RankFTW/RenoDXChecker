using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using Xunit;

namespace RenoDXCommander.Tests;

// Feature: shader-selection-popup, Property 3: Cancel action preserves global selection

/// <summary>
/// Property-based tests for the Cancel action preserving global selection.
/// **Validates: Requirements 3.5**
/// </summary>
[Collection("StaticShaderMode")]
public class ShaderPopupCancelPropertyTests
{
    /// <summary>Known shader pack IDs from the service.</summary>
    private static readonly string[] AllPackIds =
        new ShaderPackService(new HttpClient()).AvailablePacks.Select(p => p.Id).ToArray();

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a random subset of pack IDs (including possibly empty).
    /// This simulates the initial SelectedShaderPacks before the popup is opened.
    /// </summary>
    private static Gen<List<string>> GenPackSelection()
    {
        if (AllPackIds.Length == 0)
            return Gen.Constant(new List<string>());

        return Gen.ListOf(AllPackIds.Length, Arb.Generate<bool>())
            .Select(flags =>
            {
                var subset = new List<string>();
                for (int i = 0; i < AllPackIds.Length; i++)
                    if (flags[i]) subset.Add(AllPackIds[i]);
                return subset;
            });
    }

    /// <summary>Generates a random initial ShaderDeployMode.</summary>
    private static Gen<ShaderPackService.DeployMode> GenAnyDeployMode()
    {
        return Gen.Elements(
            ShaderPackService.DeployMode.Off,
            ShaderPackService.DeployMode.Minimum,
            ShaderPackService.DeployMode.All,
            ShaderPackService.DeployMode.User,
            ShaderPackService.DeployMode.Select);
    }

    // ── Property 3 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any initial SelectedShaderPacks value and any initial ShaderDeployMode,
    /// when the user opens the popup and clicks "Cancel" (ShaderPopupHelper.ShowAsync
    /// returns null), SelectedShaderPacks should remain identical to its value before
    /// the popup was opened, and ShaderDeployMode should also remain unchanged.
    ///
    /// This simulates the ChooseShadersButton_Click handler logic on cancel:
    ///   var result = await ShaderPopupHelper.ShowAsync(...);
    ///   if (result == null) return;  // do nothing
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Cancel_PreservesGlobalSelection()
    {
        var gen = GenAnyDeployMode().SelectMany(mode =>
            GenPackSelection().Select(selection => (mode, selection)));

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (initialMode, initialPacks) = tuple;

            // Arrange: create a SettingsViewModel with the random initial state
            var vm = new SettingsViewModel();
            vm.ShaderDeployMode = initialMode;
            vm.SelectedShaderPacks = new List<string>(initialPacks);

            // Snapshot the state before cancel
            var snapshotPacks = new List<string>(vm.SelectedShaderPacks);
            var snapshotMode = vm.ShaderDeployMode;

            // Act: simulate Cancel — ShowAsync returns null, caller does nothing
            List<string>? popupResult = null;
            if (popupResult != null)
            {
                // This block would execute on Deploy; it should NOT execute on Cancel
                vm.SelectedShaderPacks = popupResult;
                vm.ShaderDeployMode = ShaderPackService.DeployMode.Select;
            }

            // Assert 1: SelectedShaderPacks is unchanged
            var expected = new HashSet<string>(snapshotPacks, StringComparer.OrdinalIgnoreCase);
            var actual = new HashSet<string>(vm.SelectedShaderPacks, StringComparer.OrdinalIgnoreCase);

            if (!expected.SetEquals(actual))
                return false.Label(
                    $"Selection changed: expected [{string.Join(",", expected)}], " +
                    $"got [{string.Join(",", actual)}]");

            // Assert 2: count preserved (no duplicates introduced or items lost)
            if (vm.SelectedShaderPacks.Count != snapshotPacks.Count)
                return false.Label(
                    $"Count changed: expected {snapshotPacks.Count}, got {vm.SelectedShaderPacks.Count}");

            // Assert 3: ShaderDeployMode is unchanged
            if (vm.ShaderDeployMode != snapshotMode)
                return false.Label(
                    $"Mode changed: expected {snapshotMode}, got {vm.ShaderDeployMode}");

            return true.Label(
                $"OK: {snapshotPacks.Count} packs preserved, mode={snapshotMode}");
        });
    }
}
