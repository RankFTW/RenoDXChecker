using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based test for custom filter JSON serialization round-trip.
/// Feature: universal-search-filters, Property 9: Custom filter serialization round-trip
/// </summary>
public class CustomFilterRoundTripPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a non-empty filter name.
    /// </summary>
    private static readonly Gen<string> GenNonEmptyName =
        Gen.Elements("DX11 Games", "Steam Only", "Unreal HDR", "My Filter", "Favourites Search",
                     "VulkanGames", "LumaStuff", "32bit", "pumbo mods", "TestFilter");

    /// <summary>
    /// Generates a query string (may be empty).
    /// </summary>
    private static readonly Gen<string> GenQuery =
        Gen.Elements("dx11", "steam", "unreal", "hdr", "vulkan", "32-bit", "luma", "pumbo", "", "unity");

    /// <summary>
    /// Generates a CustomFilter with a non-empty name and a query string.
    /// </summary>
    private static readonly Gen<CustomFilter> GenCustomFilter =
        from name in GenNonEmptyName
        from query in GenQuery
        select new CustomFilter { Name = name, Query = query };

    /// <summary>
    /// Generates a list of 0–5 CustomFilter objects.
    /// </summary>
    private static readonly Gen<List<CustomFilter>> GenCustomFilterList =
        GenCustomFilter.ListOf().Select(l => l.Take(5).ToList());

    // ── Property 9 ────────────────────────────────────────────────────────────────
    // Feature: universal-search-filters, Property 9: Custom filter serialization round-trip
    // **Validates: Requirements 6.4**

    /// <summary>
    /// For any valid list of CustomFilter objects (each with a non-empty name and a
    /// query string), serializing the list to JSON and then deserializing it back
    /// should produce an equivalent list with the same names and queries in the same order.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property CustomFilter_SerializationRoundTrip_PreservesNamesAndQueries()
    {
        return Prop.ForAll(
            Arb.From(GenCustomFilterList),
            (List<CustomFilter> original) =>
            {
                // Act: serialize then deserialize using System.Text.Json (same as production code)
                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<List<CustomFilter>>(json) ?? new();

                // Assert: same count
                bool sameCount = deserialized.Count == original.Count;

                // Assert: same names and queries in the same order
                bool sameContent = original
                    .Zip(deserialized, (o, d) => o.Name == d.Name && o.Query == d.Query)
                    .All(match => match);

                return (sameCount && sameContent)
                    .Label($"sameCount={sameCount}, sameContent={sameContent} " +
                           $"(original count={original.Count}, deserialized count={deserialized.Count}, " +
                           $"json='{json}')");
            });
    }
}
