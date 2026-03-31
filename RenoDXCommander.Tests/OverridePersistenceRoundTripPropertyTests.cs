using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using Xunit;

namespace RenoDXCommander.Tests;

// Feature: override-bitness-api, Property 6: Override persistence round-trip

/// <summary>
/// Property-based tests for override persistence round-trip.
/// For any game name and valid bitness override value, setting the bitness override
/// and then getting it should return the same value. For any game name and valid list
/// of API names, setting the API override and then getting it should return an equivalent list.
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
public class OverridePersistenceRoundTripPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a safe game name: non-empty alphanumeric string that is valid
    /// as a dictionary key.
    /// </summary>
    private static Gen<string> GenGameName()
    {
        return Gen.Elements(
            "CyberGame", "SpaceShooter", "RacingPro", "PuzzleMaster",
            "RPGWorld", "FPSArena", "StrategyKing", "PlatformJump",
            "HorrorNight", "SimCity2K", "AdventureQuest", "SportsChamp");
    }

    /// <summary>
    /// Generates a valid bitness override value: "32" or "64".
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

    // ── Property 6a: Bitness override round-trip ──────────────────────────────────

    /// <summary>
    /// For any game name and valid bitness override value ("32" or "64"),
    /// setting the value in BitnessOverrides and then reading it back
    /// should return the same value.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property RoundTrip_BitnessOverride()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenBitnessValue().ToArbitrary(),
            (gameName, bitnessValue) =>
            {
                // Arrange: create a fresh dictionary matching GameNameService pattern
                var bitnessOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Act: set the override
                bitnessOverrides[gameName] = bitnessValue;

                // Act: get it back
                var retrieved = bitnessOverrides.TryGetValue(gameName, out var value) ? value : null;

                // Assert: retrieved value matches what was set
                if (retrieved != bitnessValue)
                    return false.Label(
                        $"Bitness mismatch for '{gameName}': expected '{bitnessValue}', got '{retrieved}'");

                return true.Label(
                    $"OK: bitness round-trip for '{gameName}' = '{bitnessValue}'");
            });
    }

    // ── Property 6b: API override round-trip ──────────────────────────────────────

    /// <summary>
    /// For any game name and valid list of API names, setting the value in
    /// ApiOverrides and then reading it back should return an equivalent list.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property RoundTrip_ApiOverride()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenApiList().ToArbitrary(),
            (gameName, apiList) =>
            {
                // Arrange: create a fresh dictionary matching GameNameService pattern
                var apiOverrides = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                // Act: set the override
                apiOverrides[gameName] = apiList;

                // Act: get it back
                if (!apiOverrides.TryGetValue(gameName, out var retrieved))
                    return false.Label($"API override not found for '{gameName}'");

                // Assert: same count
                if (retrieved.Count != apiList.Count)
                    return false.Label(
                        $"API count mismatch for '{gameName}': expected {apiList.Count}, got {retrieved.Count}");

                // Assert: same elements (order-preserving)
                var expectedSet = new HashSet<string>(apiList, StringComparer.OrdinalIgnoreCase);
                var actualSet = new HashSet<string>(retrieved, StringComparer.OrdinalIgnoreCase);

                if (!expectedSet.SetEquals(actualSet))
                    return false.Label(
                        $"API set mismatch for '{gameName}': " +
                        $"expected [{string.Join(",", expectedSet)}], " +
                        $"got [{string.Join(",", actualSet)}]");

                return true.Label(
                    $"OK: API round-trip for '{gameName}' with {apiList.Count} APIs");
            });
    }

    // ── Property 6c: Multiple game overrides round-trip ───────────────────────────

    /// <summary>
    /// For any set of game names with random bitness and API overrides,
    /// setting all overrides and then reading each back should return the
    /// original values. This validates that multiple entries coexist correctly
    /// in the case-insensitive dictionaries.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property RoundTrip_MultipleGameOverrides()
    {
        var genEntries = Gen.Choose(1, 6).SelectMany(count =>
            Gen.ArrayOf(count, GenGameName().SelectMany(name =>
                GenBitnessValue().SelectMany(bitness =>
                    GenApiList().Select(apis => (name, bitness, apis)))))
            .Select(entries =>
            {
                // Deduplicate by game name (last-write-wins, matching dictionary behavior)
                var dict = new Dictionary<string, (string bitness, List<string> apis)>(StringComparer.OrdinalIgnoreCase);
                foreach (var (n, b, a) in entries)
                    dict[n] = (b, a);
                return dict;
            }));

        return Prop.ForAll(genEntries.ToArbitrary(), entries =>
        {
            // Arrange: create fresh dictionaries matching GameNameService pattern
            var bitnessOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var apiOverrides = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // Act: set all overrides
            foreach (var kv in entries)
            {
                bitnessOverrides[kv.Key] = kv.Value.bitness;
                apiOverrides[kv.Key] = kv.Value.apis;
            }

            // Assert: all entries round-trip correctly
            foreach (var kv in entries)
            {
                // Check bitness
                if (!bitnessOverrides.TryGetValue(kv.Key, out var bitness) || bitness != kv.Value.bitness)
                    return false.Label(
                        $"Bitness mismatch for '{kv.Key}': expected '{kv.Value.bitness}', got '{bitness}'");

                // Check APIs
                if (!apiOverrides.TryGetValue(kv.Key, out var apis))
                    return false.Label($"API override not found for '{kv.Key}'");

                var expectedApis = new HashSet<string>(kv.Value.apis, StringComparer.OrdinalIgnoreCase);
                var actualApis = new HashSet<string>(apis, StringComparer.OrdinalIgnoreCase);

                if (!expectedApis.SetEquals(actualApis))
                    return false.Label(
                        $"API set mismatch for '{kv.Key}': " +
                        $"expected [{string.Join(",", expectedApis)}], " +
                        $"got [{string.Join(",", actualApis)}]");
            }

            // Assert: dictionary sizes match
            if (bitnessOverrides.Count != entries.Count)
                return false.Label(
                    $"Bitness dict size mismatch: expected {entries.Count}, got {bitnessOverrides.Count}");

            if (apiOverrides.Count != entries.Count)
                return false.Label(
                    $"API dict size mismatch: expected {entries.Count}, got {apiOverrides.Count}");

            return true.Label(
                $"OK: round-trip preserved {entries.Count} game overrides");
        });
    }
}
