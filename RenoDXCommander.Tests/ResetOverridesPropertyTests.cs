using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: override-bitness-api, Property 7: Reset removes all overrides and restores auto-detected values

/// <summary>
/// Property-based tests for reset removing all overrides.
/// For any game name with any combination of bitness and API overrides set,
/// after performing a reset (SetBitnessOverride(gameName, null) and
/// SetApiOverride(gameName, null)), the BitnessOverrides dictionary should not
/// contain an entry for that game, the ApiOverrides dictionary should not contain
/// an entry for that game, and the resolved Is32Bit and DetectedApis should equal
/// their auto-detected values.
/// **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
/// </summary>
public class ResetOverridesPropertyTests
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
    /// Generates a bitness override value: "32" or "64".
    /// </summary>
    private static Gen<string> GenBitnessValue()
    {
        return Gen.Elements("32", "64");
    }

    /// <summary>
    /// All non-Unknown GraphicsApiType names for generating API override lists.
    /// </summary>
    private static readonly string[] AllApiNames = Enum.GetValues<GraphicsApiType>()
        .Where(a => a != GraphicsApiType.Unknown)
        .Select(a => a.ToString())
        .ToArray();

    /// <summary>
    /// Generates a random non-empty subset of GraphicsApiType names.
    /// </summary>
    private static Gen<List<string>> GenApiList()
    {
        return Gen.ListOf(AllApiNames.Length, Arb.Generate<bool>())
            .Where(flags => flags.Any(f => f)) // ensure at least one API is selected
            .Select(flags =>
            {
                var subset = new List<string>();
                for (int i = 0; i < AllApiNames.Length; i++)
                    if (flags[i]) subset.Add(AllApiNames[i]);
                return subset;
            });
    }

    // ── Property 7a: Reset removes bitness override from dictionary ────────────

    /// <summary>
    /// For any game name and any bitness override value ("32" or "64"),
    /// setting the override and then resetting it via SetBitnessOverride(gameName, null)
    /// should remove the entry from the BitnessOverrides dictionary and
    /// GetBitnessOverride should return null.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Reset_RemovesBitnessOverride()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenBitnessValue().ToArbitrary(),
            (gameName, bitnessValue) =>
            {
                // Arrange: create a fresh ViewModel and set a bitness override
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetBitnessOverride(gameName, bitnessValue);

                // Verify override was set
                var before = vm.GetBitnessOverride(gameName);
                if (before != bitnessValue)
                    return false.Label(
                        $"Setup failed: expected override '{bitnessValue}', got '{before}'");

                // Act: reset by setting null
                vm.SetBitnessOverride(gameName, null);

                // Assert: override is removed
                var after = vm.GetBitnessOverride(gameName);
                if (after != null)
                    return false.Label(
                        $"Reset failed for '{gameName}': expected null, got '{after}'");

                // Assert: dictionary does not contain the key
                if (vm.GameNameServiceInstance.BitnessOverrides.ContainsKey(gameName))
                    return false.Label(
                        $"Dictionary still contains key '{gameName}' after reset");

                return true.Label(
                    $"OK: bitness reset for '{gameName}' (was '{bitnessValue}')");
            });
    }

    // ── Property 7b: Reset removes API override from dictionary ───────────────

    /// <summary>
    /// For any game name and any non-empty API override list,
    /// setting the override and then resetting it via SetApiOverride(gameName, null)
    /// should remove the entry from the ApiOverrides dictionary and
    /// GetApiOverride should return null.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Reset_RemovesApiOverride()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenApiList().ToArbitrary(),
            (gameName, apiList) =>
            {
                // Arrange: create a fresh ViewModel and set an API override
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, apiList);

                // Verify override was set
                var before = vm.GetApiOverride(gameName);
                if (before == null)
                    return false.Label(
                        $"Setup failed: API override not found for '{gameName}'");

                // Act: reset by setting null
                vm.SetApiOverride(gameName, null);

                // Assert: override is removed
                var after = vm.GetApiOverride(gameName);
                if (after != null)
                    return false.Label(
                        $"Reset failed for '{gameName}': expected null, got [{string.Join(",", after)}]");

                // Assert: dictionary does not contain the key
                if (vm.GameNameServiceInstance.ApiOverrides.ContainsKey(gameName))
                    return false.Label(
                        $"Dictionary still contains key '{gameName}' after reset");

                return true.Label(
                    $"OK: API reset for '{gameName}' (had {apiList.Count} APIs)");
            });
    }

    // ── Property 7c: Reset removes both overrides simultaneously ──────────────

    /// <summary>
    /// For any game name with both bitness and API overrides set,
    /// resetting both should remove all entries and restore auto-detected values.
    /// GetBitnessOverride returns null, GetApiOverride returns null,
    /// and neither dictionary contains the game key.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Reset_RemovesBothOverrides()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenBitnessValue().ToArbitrary(),
            GenApiList().ToArbitrary(),
            (gameName, bitnessValue, apiList) =>
            {
                // Arrange: create a fresh ViewModel and set both overrides
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetBitnessOverride(gameName, bitnessValue);
                vm.SetApiOverride(gameName, apiList);

                // Act: reset both
                vm.SetBitnessOverride(gameName, null);
                vm.SetApiOverride(gameName, null);

                // Assert: bitness override removed
                if (vm.GetBitnessOverride(gameName) != null)
                    return false.Label(
                        $"Bitness override not removed for '{gameName}'");

                if (vm.GameNameServiceInstance.BitnessOverrides.ContainsKey(gameName))
                    return false.Label(
                        $"BitnessOverrides dict still contains '{gameName}'");

                // Assert: API override removed
                if (vm.GetApiOverride(gameName) != null)
                    return false.Label(
                        $"API override not removed for '{gameName}'");

                if (vm.GameNameServiceInstance.ApiOverrides.ContainsKey(gameName))
                    return false.Label(
                        $"ApiOverrides dict still contains '{gameName}'");

                return true.Label(
                    $"OK: both overrides reset for '{gameName}'");
            });
    }

    // ── Property 7d: Reset restores auto-detected bitness ─────────────────────

    /// <summary>
    /// For any game name and MachineType, after setting a bitness override and
    /// then resetting it, ResolveIs32Bit should return the auto-detected value
    /// (machineType == I386) rather than the override value.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Reset_RestoresAutoDetectedBitness()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenBitnessValue().ToArbitrary(),
            Gen.Elements(MachineType.I386, MachineType.x64).ToArbitrary(),
            (gameName, bitnessValue, machineType) =>
            {
                // Arrange: create a fresh ViewModel and set a bitness override
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetBitnessOverride(gameName, bitnessValue);

                // Act: reset
                vm.SetBitnessOverride(gameName, null);

                // Act: resolve bitness — should use auto-detection
                bool result = vm.ResolveIs32Bit(gameName, machineType);
                bool expected = machineType == MachineType.I386;

                if (result != expected)
                    return false.Label(
                        $"After reset, ResolveIs32Bit for '{gameName}' with {machineType}: " +
                        $"expected {expected}, got {result}");

                return true.Label(
                    $"OK: after reset, '{gameName}' with {machineType} → Is32Bit={result}");
            });
    }

    // ── Property 7e: Reset restores auto-detected APIs ────────────────────────

    /// <summary>
    /// For any game name, after setting an API override and then resetting it,
    /// _DetectAllApisForCard should fall through to scanning (empty set for
    /// non-existent path) rather than returning the override set.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Reset_RestoresAutoDetectedApis()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenApiList().ToArbitrary(),
            (gameName, apiList) =>
            {
                // Arrange: create a fresh ViewModel and set an API override
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, apiList);

                // Act: reset
                vm.SetApiOverride(gameName, null);

                // Act: detect APIs — should fall through to scanning (empty for fake path)
                var result = vm._DetectAllApisForCard(@"C:\NonExistent\Path\12345", gameName);

                if (result.Count != 0)
                    return false.Label(
                        $"After reset, expected empty API set for '{gameName}', " +
                        $"got [{string.Join(",", result)}]");

                return true.Label(
                    $"OK: after reset, '{gameName}' → empty API set (auto-detect)");
            });
    }
}
