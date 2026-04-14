// Feature: nexus-pcgw-integration, Property 9: Manifest new fields serialization round-trip
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for manifest override fields serialization round-trip.
/// Verifies that nexusUrlOverrides, steamAppIdOverrides, and pcgwUrlOverrides
/// survive JSON serialize → deserialize without data loss.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
/// </summary>
public class ManifestOverrideRoundTripPropertyTests
{
    /// <summary>
    /// Generator for non-null dictionaries of string → string with printable keys/values.
    /// Produces dictionaries with 0–5 entries to keep tests fast.
    /// </summary>
    private static Arbitrary<Dictionary<string, string>?> NullableStringDictArb()
    {
        var entryGen =
            from k in Arb.Default.NonEmptyString().Generator.Select(s => s.Get)
            from v in Arb.Default.NonEmptyString().Generator.Select(s => s.Get)
            select new KeyValuePair<string, string>(k, v);

        var dictGen = Gen.OneOf(
            Gen.Constant<Dictionary<string, string>?>(null),
            Gen.ListOf(entryGen).Select(pairs =>
            {
                var dict = new Dictionary<string, string>();
                foreach (var kvp in pairs)
                    dict[kvp.Key] = kvp.Value; // last-write-wins deduplicates keys
                return (Dictionary<string, string>?)dict;
            })
        );

        return dictGen.ToArbitrary();
    }

    /// <summary>
    /// Generator for nullable dictionaries of string → int with printable keys.
    /// Produces dictionaries with 0–5 entries.
    /// </summary>
    private static Arbitrary<Dictionary<string, int>?> NullableIntDictArb()
    {
        var entryGen =
            from k in Arb.Default.NonEmptyString().Generator.Select(s => s.Get)
            from v in Arb.Default.Int32().Generator
            select new KeyValuePair<string, int>(k, v);

        var dictGen = Gen.OneOf(
            Gen.Constant<Dictionary<string, int>?>(null),
            Gen.ListOf(entryGen).Select(pairs =>
            {
                var dict = new Dictionary<string, int>();
                foreach (var kvp in pairs)
                    dict[kvp.Key] = kvp.Value;
                return (Dictionary<string, int>?)dict;
            })
        );

        return dictGen.ToArbitrary();
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 9: Manifest new fields serialization round-trip
    ///
    /// **Validates: Requirements 3.1, 5a.1, 7a.1**
    ///
    /// For any RemoteManifest with arbitrary nexusUrlOverrides, steamAppIdOverrides,
    /// and pcgwUrlOverrides, serializing to JSON and deserializing produces an
    /// equivalent manifest with identical override dictionaries.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ManifestOverrideFields_SurviveJsonRoundTrip()
    {
        return Prop.ForAll(
            NullableStringDictArb(),
            NullableIntDictArb(),
            NullableStringDictArb(),
            (Dictionary<string, string>? nexusOverrides,
             Dictionary<string, int>? steamOverrides,
             Dictionary<string, string>? pcgwOverrides) =>
            {
                // Arrange
                var original = new RemoteManifest
                {
                    NexusUrlOverrides = nexusOverrides,
                    SteamAppIdOverrides = steamOverrides,
                    PcgwUrlOverrides = pcgwOverrides,
                };

                // Act: serialize → deserialize
                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<RemoteManifest>(json);

                // Assert
                var nexusOk = DictionariesEqual(original.NexusUrlOverrides, deserialized?.NexusUrlOverrides);
                var steamOk = DictionariesEqual(original.SteamAppIdOverrides, deserialized?.SteamAppIdOverrides);
                var pcgwOk = DictionariesEqual(original.PcgwUrlOverrides, deserialized?.PcgwUrlOverrides);

                return (nexusOk && steamOk && pcgwOk)
                    .Label($"nexusOk={nexusOk}, steamOk={steamOk}, pcgwOk={pcgwOk}");
            });
    }

    /// <summary>
    /// Compares two nullable dictionaries for value equality.
    /// Both null → equal. One null, other empty → not equal (serialization preserves null vs empty).
    /// </summary>
    private static bool DictionariesEqual<TValue>(
        Dictionary<string, TValue>? a,
        Dictionary<string, TValue>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var bVal)) return false;
            if (!EqualityComparer<TValue>.Default.Equals(kvp.Value, bVal)) return false;
        }
        return true;
    }
}
