using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;

namespace RenoDXCommander.Tests;

// Feature: override-bitness-api, Property 3: API resolution respects override priority

/// <summary>
/// Property-based tests for API resolution priority.
/// For any game name, any auto-detected API set, and any API override set (or absence
/// thereof), the effective DetectedApis should exactly equal the override set when an
/// override entry exists for the game, or the auto-detected set when no override entry exists.
/// **Validates: Requirements 3.3, 3.4, 3.5, 3.6, 4.4**
/// </summary>
public class ApiResolutionPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a safe game name: non-empty alphanumeric string that is valid
    /// as a dictionary key and won't collide with manifest overrides.
    /// </summary>
    private static Gen<string> GenGameName()
    {
        return Gen.Elements(
            "CyberGame", "SpaceShooter", "RacingPro", "PuzzleMaster",
            "RPGWorld", "FPSArena", "StrategyKing", "PlatformJump",
            "HorrorNight", "SimCity2K", "AdventureQuest", "SportsChamp");
    }

    /// <summary>
    /// All non-Unknown GraphicsApiType values for generating API sets.
    /// </summary>
    private static readonly GraphicsApiType[] AllApis = Enum.GetValues<GraphicsApiType>()
        .Where(a => a != GraphicsApiType.Unknown)
        .ToArray();

    /// <summary>
    /// Generates a random subset of GraphicsApiType names (possibly empty).
    /// </summary>
    private static Gen<List<string>> GenApiList()
    {
        return Gen.ListOf(AllApis.Length, Arb.Generate<bool>())
            .Select(flags =>
            {
                var flagList = flags.ToList();
                var subset = new List<string>();
                for (int i = 0; i < AllApis.Length && i < flagList.Count; i++)
                    if (flagList[i]) subset.Add(AllApis[i].ToString());
                return subset;
            });
    }

    /// <summary>
    /// Generates a non-empty subset of GraphicsApiType names for override sets.
    /// </summary>
    private static Gen<List<string>> GenNonEmptyApiList()
    {
        return GenApiList().Where(list => list.Count > 0);
    }

    // ── Property 3a: Override set takes priority over scanning ─────────────────

    /// <summary>
    /// When an API override is set for a game, _DetectAllApisForCard returns
    /// exactly the override set (parsed via Enum.TryParse). The install path
    /// is a non-existent directory so scanning returns nothing — the override
    /// must be the sole source of the result.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property DetectAllApis_WithOverride_ReturnsOverrideSet()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenNonEmptyApiList().ToArbitrary(),
            (gameName, overrideApis) =>
            {
                // Arrange
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, overrideApis);

                // Act: call _DetectAllApisForCard with a non-existent path
                var result = vm._DetectAllApisForCard(@"C:\NonExistent\Path\12345", gameName);

                // Expected: parse the override list the same way the method does
                var expected = new HashSet<GraphicsApiType>();
                foreach (var name in overrideApis)
                {
                    if (Enum.TryParse<GraphicsApiType>(name, out var apiType))
                        expected.Add(apiType);
                }

                if (!result.SetEquals(expected))
                    return false.Label(
                        $"Override mismatch for '{gameName}': " +
                        $"expected [{string.Join(",", expected)}], " +
                        $"got [{string.Join(",", result)}]");

                return true.Label(
                    $"OK: override for '{gameName}' = [{string.Join(",", result)}]");
            });
    }

    // ── Property 3b: No override falls through to scanning (empty for fake path) ─

    /// <summary>
    /// When no API override exists for a game, _DetectAllApisForCard falls through
    /// to file system scanning. With a non-existent install path and no manifest,
    /// the result should be an empty set.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property DetectAllApis_WithoutOverride_FallsThrough()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            (gameName) =>
            {
                // Arrange: explicitly remove any override that may have been persisted
                // by previous test runs, so we test the true "no override" path
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, null);

                // Act: call with non-existent path
                var result = vm._DetectAllApisForCard(@"C:\NonExistent\Path\12345", gameName);

                // Expected: empty set (no files to scan, no manifest, no override)
                if (result.Count != 0)
                    return false.Label(
                        $"Expected empty set for '{gameName}' with no override, " +
                        $"got [{string.Join(",", result)}]");

                return true.Label(
                    $"OK: no override for '{gameName}' → empty set");
            });
    }

    // ── Property 3c: Null override removal restores scanning behavior ─────────

    /// <summary>
    /// Setting an API override and then removing it (null) should restore the
    /// scanning behavior — with a non-existent path, the result is empty.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property DetectAllApis_OverrideRemoved_RestoresScanning()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenNonEmptyApiList().ToArbitrary(),
            (gameName, overrideApis) =>
            {
                // Arrange: set then remove override
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, overrideApis);
                vm.SetApiOverride(gameName, null); // remove

                // Act
                var result = vm._DetectAllApisForCard(@"C:\NonExistent\Path\12345", gameName);

                // Expected: empty (override removed, no files to scan)
                if (result.Count != 0)
                    return false.Label(
                        $"Expected empty set after override removal for '{gameName}', " +
                        $"got [{string.Join(",", result)}]");

                return true.Label(
                    $"OK: override removed for '{gameName}' → empty set");
            });
    }

    // ── Property 3d: Override with gameName=null bypasses override check ───────

    /// <summary>
    /// When gameName is null, _DetectAllApisForCard should skip the override
    /// check entirely and fall through to scanning (empty for non-existent path).
    /// This validates that the override lookup is gated on gameName != null.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property DetectAllApis_NullGameName_SkipsOverride()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenNonEmptyApiList().ToArbitrary(),
            (gameName, overrideApis) =>
            {
                // Arrange: set override for a game
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, overrideApis);

                // Act: call with null gameName — should skip override
                var result = vm._DetectAllApisForCard(@"C:\NonExistent\Path\12345", null);

                // Expected: empty (null gameName skips override, no files to scan)
                if (result.Count != 0)
                    return false.Label(
                        $"Expected empty set when gameName is null, " +
                        $"got [{string.Join(",", result)}]");

                return true.Label(
                    $"OK: null gameName → empty set (override skipped)");
            });
    }
}
