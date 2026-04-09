using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based test for NormalReShadeGames toggle persistence round-trip.
/// Feature: reshade-no-addon-support, Property 1: Toggle persistence round-trip
/// **Validates: Requirements 1.2, 1.3, 1.4**
/// </summary>
public class NormalReShadeTogglePersistenceRoundTripPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates game names including ASCII letters, digits, spaces, special chars,
    /// mixed case, and Unicode characters.
    /// </summary>
    private static readonly Gen<string> GenGameName =
        from len in Gen.Choose(1, 40)
        from chars in Gen.ArrayOf(len, Gen.Frequency(
            Tuple.Create(5, Gen.Elements(
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray())),
            Tuple.Create(2, Gen.Elements(
                "0123456789".ToCharArray())),
            Tuple.Create(2, Gen.Elements(
                " -_:()[]!@#&'.".ToCharArray())),
            Tuple.Create(1, Gen.Elements(
                "àéîöüñçÄÖÜß日本語中文한국".ToCharArray()))))
        select new string(chars);

    /// <summary>
    /// Generates a non-empty set of distinct game names (1–10 games).
    /// </summary>
    private static readonly Gen<HashSet<string>> GenGameNameSet =
        from count in Gen.Choose(1, 10)
        from names in Gen.ArrayOf(count, GenGameName)
        select new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

    // ── Property 1: Toggle persistence round-trip ─────────────────────────────────
    // Feature: reshade-no-addon-support, Property 1: Toggle persistence round-trip
    // **Validates: Requirements 1.2, 1.3, 1.4**

    /// <summary>
    /// For any set of game names (special chars, mixed case, Unicode),
    /// serializing as List&lt;string&gt; → deserializing into HashSet&lt;string&gt;
    /// with OrdinalIgnoreCase produces an equivalent set.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SerializeDeserialize_ProducesEquivalentSet()
    {
        return Prop.ForAll(
            Arb.From(GenGameNameSet),
            (HashSet<string> original) =>
            {
                // Serialize as List<string> — same as SaveNameMappings
                var json = JsonSerializer.Serialize(original.ToList());

                // Deserialize back into HashSet with OrdinalIgnoreCase — same as LoadNameMappings
                var deserialized = new HashSet<string>(
                    JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                // Sets should be equivalent (same count, same members ignoring case)
                if (original.Count != deserialized.Count)
                    return false.Label(
                        $"Count mismatch: original={original.Count}, deserialized={deserialized.Count}");

                foreach (var name in original)
                {
                    if (!deserialized.Contains(name))
                        return false.Label(
                            $"Missing game '{name}' after round-trip");
                }

                return true.Label("Round-trip preserved all game names");
            });
    }

    /// <summary>
    /// For any set of game names, removing a game and re-serializing should
    /// produce a set that does not contain the removed game.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RemoveGame_ThenRoundTrip_ExcludesRemovedGame()
    {
        var gen =
            from names in GenGameNameSet
            where names.Count > 0
            from indexToRemove in Gen.Choose(0, names.Count - 1)
            select (names, indexToRemove);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (original, indexToRemove) = tuple;
            var gameToRemove = original.ElementAt(indexToRemove);

            // Remove the game (simulates user disabling the toggle — Req 1.4)
            var modified = new HashSet<string>(original, StringComparer.OrdinalIgnoreCase);
            modified.Remove(gameToRemove);

            // Serialize the modified set
            var json = JsonSerializer.Serialize(modified.ToList());

            // Deserialize back
            var deserialized = new HashSet<string>(
                JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            // The removed game must be absent
            if (deserialized.Contains(gameToRemove))
                return false.Label(
                    $"Removed game '{gameToRemove}' still present after re-serialization");

            // Remaining games should all be present
            if (modified.Count != deserialized.Count)
                return false.Label(
                    $"Count mismatch: modified={modified.Count}, deserialized={deserialized.Count}");

            foreach (var name in modified)
            {
                if (!deserialized.Contains(name))
                    return false.Label(
                        $"Missing game '{name}' after round-trip of modified set");
            }

            return true.Label("Removed game excluded and remaining games preserved");
        });
    }
}
