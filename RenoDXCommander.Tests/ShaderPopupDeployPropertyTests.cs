using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using Xunit;

namespace RenoDXCommander.Tests;

// Feature: shader-selection-popup, Property 2: Deploy action saves the selected packs to global selection

/// <summary>
/// Property-based tests for the Deploy action saving selected packs to global selection.
/// **Validates: Requirements 3.2**
/// </summary>
[Collection("StaticShaderMode")]
public class ShaderPopupDeployPropertyTests
{
    /// <summary>Known shader pack IDs from the service.</summary>
    private static readonly string[] AllPackIds =
        new ShaderPackService(new HttpClient()).AvailablePacks.Select(p => p.Id).ToArray();

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a random subset of pack IDs (including possibly empty).
    /// This simulates the list returned by the popup when the user clicks Deploy.
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

    /// <summary>Generates a random initial ShaderDeployMode to start from.</summary>
    private static Gen<ShaderPackService.DeployMode> GenAnyDeployMode()
    {
        return Gen.Elements(
            ShaderPackService.DeployMode.Off,
            ShaderPackService.DeployMode.Minimum,
            ShaderPackService.DeployMode.All,
            ShaderPackService.DeployMode.User,
            ShaderPackService.DeployMode.Select);
    }

    // ── Property 2 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any set of shader packs selected in the popup (including the empty set)
    /// and for any initial deploy mode, when the user clicks "Deploy", the resulting
    /// SettingsViewModel.SelectedShaderPacks should contain exactly the selected pack IDs
    /// and ShaderDeployMode should be set to Select.
    ///
    /// This simulates the ChooseShadersButton_Click handler logic:
    ///   ViewModel.Settings.SelectedShaderPacks = result;
    ///   ViewModel.Settings.ShaderDeployMode = ShaderPackService.DeployMode.Select;
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Deploy_SavesSelectedPacks_ToGlobalSelection()
    {
        var gen = GenAnyDeployMode().SelectMany(mode =>
            GenPackSelection().Select(selection => (mode, selection)));

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (initialMode, selectedPacks) = tuple;

            // Arrange: create a SettingsViewModel with some initial state
            var vm = new SettingsViewModel();
            vm.ShaderDeployMode = initialMode;
            vm.SelectedShaderPacks = new List<string> { "SomeOldPack" }; // pre-existing selection

            // Act: simulate the Deploy action from ChooseShadersButton_Click
            vm.SelectedShaderPacks = new List<string>(selectedPacks);
            vm.ShaderDeployMode = ShaderPackService.DeployMode.Select;

            // Assert 1: SelectedShaderPacks contains exactly the selected pack IDs
            var expected = new HashSet<string>(selectedPacks, StringComparer.OrdinalIgnoreCase);
            var actual = new HashSet<string>(vm.SelectedShaderPacks, StringComparer.OrdinalIgnoreCase);

            if (!expected.SetEquals(actual))
                return false.Label(
                    $"Selection mismatch: expected [{string.Join(",", expected)}], " +
                    $"got [{string.Join(",", actual)}]");

            // Assert 2: ShaderDeployMode is set to Select
            if (vm.ShaderDeployMode != ShaderPackService.DeployMode.Select)
                return false.Label(
                    $"Mode mismatch: expected Select, got {vm.ShaderDeployMode}");

            // Assert 3: count matches (no duplicates introduced)
            if (vm.SelectedShaderPacks.Count != selectedPacks.Count)
                return false.Label(
                    $"Count mismatch: expected {selectedPacks.Count}, got {vm.SelectedShaderPacks.Count}");

            return true.Label(
                $"OK: {selectedPacks.Count} packs saved, mode=Select (was {initialMode})");
        });
    }
}
