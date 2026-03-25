using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for shader resolution priority chain.
/// Feature: custom-shaders, Property 3: Shader resolution follows priority chain
/// **Validates: Requirements 2.1, 2.2, 4.4, 5.1, 5.2, 5.3, 6.1**
/// </summary>
public class ShaderResolutionPriorityPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenGameName =
        Gen.Elements(
            "Cyberpunk 2077", "Elden Ring", "Starfield", "Hogwarts Legacy",
            "Alan Wake 2", "Baldur's Gate 3", "Final Fantasy XVI",
            "The Witcher 3", "Red Dead Redemption 2", "Halo Infinite");

    private static readonly Gen<string> GenShaderMode =
        Gen.Elements("Custom", "Select", "Global");

    private static readonly Gen<string> GenPackId =
        Gen.Elements("Lilium", "qUINT", "AstrayFX", "SweetFX", "MagicHDR", "CorgiFX");

    private static Gen<List<string>> GenPackList() =>
        from count in Gen.Choose(0, 4)
        from packs in Gen.ListOf(count, GenPackId)
        select packs.Distinct().ToList();

    /// <summary>
    /// Generates a tuple of (globalCustomEnabled, perGameMode, perGamePacks, globalPacks).
    /// perGameMode is null when the game has no per-game override (falls through to global).
    /// </summary>
    private static Gen<(bool GlobalCustom, string? PerGameMode, List<string> PerGamePacks, List<string> GlobalPacks)> GenResolutionInput() =>
        from globalCustom in Arb.Default.Bool().Generator
        from hasPerGameMode in Arb.Default.Bool().Generator
        from perGameMode in GenShaderMode
        from perGamePacks in GenPackList()
        from globalPacks in GenPackList()
        select (globalCustom, hasPerGameMode ? perGameMode : (string?)null, perGamePacks, globalPacks);

    // ── Property 3: Shader resolution follows priority chain ──────────────────────

    /// <summary>
    /// For any game name, any global UseCustomShaders state, any per-game shader mode
    /// ("Custom", "Select", "Global", or absent), and any per-game/global pack selections,
    /// ResolveShaderSelection shall return:
    /// - The custom shader sentinel when per-game mode is "Custom"
    /// - The per-game pack selection when per-game mode is "Select"
    /// - The custom shader sentinel when per-game mode is "Global"/absent AND global custom is true
    /// - The global pack selection when per-game mode is "Global"/absent AND global custom is false
    /// **Validates: Requirements 2.1, 2.2, 4.4, 5.1, 5.2, 5.3, 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResolveShaderSelection_FollowsPriorityChain()
    {
        return Prop.ForAll(
            GenGameName.ToArbitrary(),
            GenResolutionInput().ToArbitrary(),
            (string gameName, (bool GlobalCustom, string? PerGameMode, List<string> PerGamePacks, List<string> GlobalPacks) input) =>
            {
                // Arrange: create MainViewModel via TestHelpers
                var vm = TestHelpers.CreateMainViewModel();

                // Set global UseCustomShaders
                vm.Settings.IsLoadingSettings = true;
                vm.Settings.UseCustomShaders = input.GlobalCustom;
                vm.Settings.SelectedShaderPacks = input.GlobalPacks;
                vm.Settings.IsLoadingSettings = false;

                // Set per-game shader selection if mode is "Select"
                if (input.PerGameMode == "Select")
                {
                    vm.GameNameServiceInstance.PerGameShaderSelection[gameName] = input.PerGamePacks;
                }

                // The shaderModeOverride passed to ResolveShaderSelection:
                // null means no per-game override (equivalent to "Global")
                string? shaderModeOverride = input.PerGameMode switch
                {
                    "Global" => null, // "Global" means fall through — no override
                    null => null,
                    _ => input.PerGameMode
                };

                // Act
                var result = vm.ResolveShaderSelection(gameName, shaderModeOverride);

                // Assert based on priority chain
                var sentinel = new[] { ShaderPackService.CustomShaderSentinel };

                // Priority 1: Per-game "Custom" → sentinel
                if (input.PerGameMode == "Custom")
                {
                    if (result == null || !result.SequenceEqual(sentinel))
                        return false.Label(
                            $"Per-game Custom mode should return sentinel, got: [{FormatResult(result)}]");
                    return true.Label("OK: Per-game Custom → sentinel");
                }

                // Priority 2: Per-game "Select" → per-game packs
                if (input.PerGameMode == "Select")
                {
                    var expected = input.PerGamePacks;
                    if (result == null || !result.SequenceEqual(expected))
                        return false.Label(
                            $"Per-game Select mode should return per-game packs [{string.Join(",", expected)}], " +
                            $"got: [{FormatResult(result)}]");
                    return true.Label("OK: Per-game Select → per-game packs");
                }

                // Priority 3: Global custom enabled → sentinel
                if (input.GlobalCustom)
                {
                    if (result == null || !result.SequenceEqual(sentinel))
                        return false.Label(
                            $"Global custom ON should return sentinel, got: [{FormatResult(result)}]");
                    return true.Label("OK: Global custom ON → sentinel");
                }

                // Priority 4: Fallback → global packs
                if (result == null || !result.SequenceEqual(input.GlobalPacks))
                    return false.Label(
                        $"Fallback should return global packs [{string.Join(",", input.GlobalPacks)}], " +
                        $"got: [{FormatResult(result)}]");

                return true.Label("OK: Fallback → global packs");
            });
    }

    private static string FormatResult(IEnumerable<string>? result) =>
        result == null ? "null" : string.Join(",", result);
}
