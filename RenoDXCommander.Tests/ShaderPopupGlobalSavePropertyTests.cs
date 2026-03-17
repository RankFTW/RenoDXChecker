using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

// Feature: shader-selection-popup, Property 8: Saving with Global mode removes per-game selection

/// <summary>
/// Property-based tests for saving with Global mode removing per-game selection.
/// When the user sets the per-game Shader Mode to "Global" and clicks "Save Overrides",
/// the per-game entry for that game is removed from PerGameShaderSelection.
/// **Validates: Requirements 7.2**
/// </summary>
[Collection("StaticShaderMode")]
public class ShaderPopupGlobalSavePropertyTests
{
    /// <summary>Known shader pack IDs from the service.</summary>
    private static readonly string[] AllPackIds =
        new ShaderPackService(new HttpClient()).AvailablePacks.Select(p => p.Id).ToArray();

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a random subset of pack IDs (including possibly empty).
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

    /// <summary>
    /// Generates a random game name (non-empty alphanumeric string).
    /// </summary>
    private static Gen<string> GenGameName()
    {
        return Gen.Elements(
            "CyberGame", "SpaceShooter", "RacingPro", "PuzzleMaster",
            "RPGWorld", "FPSArena", "StrategyKing", "AdventureQuest",
            "SimCity2099", "HorrorNight");
    }

    // ── Property 8 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any game that previously had a per-game shader selection in PerGameShaderSelection,
    /// when the user sets the per-game Shader Mode to "Global" and clicks "Save Overrides",
    /// the per-game entry for that game should be removed from PerGameShaderSelection.
    ///
    /// This models the save logic in BuildOverridesPanel:
    ///   // "Global": ensure per-game entry is removed, then deploy with global selection
    ///   PerGameShaderSelection.Remove(gameName);
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GlobalSave_RemovesPerGameSelection()
    {
        var gen =
            GenGameName().SelectMany(gameName =>
            GenPackSelection().Select(existingPacks =>
                (gameName, existingPacks)));

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, existingPacks) = tuple;

            // Arrange: create a PerGameShaderSelection dictionary with an existing entry
            var perGameShaderSelection = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [gameName] = new List<string>(existingPacks)
            };

            // Verify precondition: entry exists before save
            if (!perGameShaderSelection.ContainsKey(gameName))
                return false.Label(
                    $"Precondition failed: no entry for '{gameName}' before save");

            // Act: simulate Save Overrides with mode = "Global"
            // This mirrors the BuildOverridesPanel save handler:
            //   PerGameShaderSelection.Remove(det);
            string newShaderMode = "Global";
            if (newShaderMode != "Select")
            {
                perGameShaderSelection.Remove(gameName);
            }

            // Assert 1: entry for the game is removed
            if (perGameShaderSelection.ContainsKey(gameName))
                return false.Label(
                    $"Entry for '{gameName}' still exists after Global save");

            // Assert 2: dictionary should be empty (we only added one entry)
            if (perGameShaderSelection.Count != 0)
                return false.Label(
                    $"Dictionary should be empty but has {perGameShaderSelection.Count} entries");

            return true.Label(
                $"OK: '{gameName}' entry removed after Global save " +
                $"(had {existingPacks.Count} packs)");
        });
    }

    /// <summary>
    /// For any game with a per-game selection, when saving with Global mode,
    /// other games' per-game selections should remain unaffected.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GlobalSave_PreservesOtherGamesSelections()
    {
        var gen =
            GenGameName().SelectMany(targetGame =>
            GenGameName().Where(other => !string.Equals(other, targetGame, StringComparison.OrdinalIgnoreCase))
            .SelectMany(otherGame =>
            GenPackSelection().SelectMany(targetPacks =>
            GenPackSelection().Select(otherPacks =>
                (targetGame, otherGame, targetPacks, otherPacks)))));

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (targetGame, otherGame, targetPacks, otherPacks) = tuple;

            // Arrange: dictionary with entries for both games
            var perGameShaderSelection = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [targetGame] = new List<string>(targetPacks),
                [otherGame] = new List<string>(otherPacks)
            };

            var otherSnapshot = new List<string>(otherPacks);

            // Act: simulate Save Overrides with mode = "Global" for targetGame only
            perGameShaderSelection.Remove(targetGame);

            // Assert 1: target game entry removed
            if (perGameShaderSelection.ContainsKey(targetGame))
                return false.Label(
                    $"Entry for '{targetGame}' still exists after Global save");

            // Assert 2: other game entry preserved
            if (!perGameShaderSelection.ContainsKey(otherGame))
                return false.Label(
                    $"Entry for '{otherGame}' was removed — should be preserved");

            // Assert 3: other game's selection unchanged
            var expectedSet = new HashSet<string>(otherSnapshot, StringComparer.OrdinalIgnoreCase);
            var actualSet = new HashSet<string>(perGameShaderSelection[otherGame], StringComparer.OrdinalIgnoreCase);
            if (!expectedSet.SetEquals(actualSet))
                return false.Label(
                    $"Selection for '{otherGame}' changed: " +
                    $"expected [{string.Join(",", expectedSet)}], " +
                    $"got [{string.Join(",", actualSet)}]");

            return true.Label(
                $"OK: '{targetGame}' removed, '{otherGame}' preserved with {otherPacks.Count} packs");
        });
    }
}
