// Feature: nexus-pcgw-integration, Property 1: Nexus dictionary round-trip
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for Nexus dictionary round-trip.
/// For any list of NexusModsGame entries with distinct normalized names,
/// building the dictionary and querying with NormalizeName(entry.Name)
/// returns the corresponding URL.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
/// </summary>
public class NexusDictionaryRoundTripPropertyTests
{
    private static readonly GameDetectionService _gameDetection = new();

    /// <summary>
    /// Generator for a list of NexusModsGame entries whose normalized names are distinct.
    /// Produces entries with non-empty alphanumeric names and non-empty URLs.
    /// </summary>
    private static Arbitrary<List<NexusModsGame>> DistinctNexusModsGameListArb()
    {
        var entryGen =
            from name in Arb.Default.NonEmptyString().Generator.Select(s => s.Get)
            from url in Arb.Default.NonEmptyString().Generator.Select(s => "https://www.nexusmods.com/" + s.Get)
            select new NexusModsGame { Name = name, NexusmodsUrl = url };

        var listGen = Gen.ListOf(entryGen).Select(entries =>
        {
            // Deduplicate by normalized name, keeping first entry per key
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var distinct = new List<NexusModsGame>();
            foreach (var e in entries)
            {
                var norm = _gameDetection.NormalizeName(e.Name);
                if (!string.IsNullOrEmpty(norm) && seen.Add(norm))
                    distinct.Add(e);
            }
            return distinct;
        });

        return listGen.ToArbitrary();
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 1: Nexus dictionary round-trip
    ///
    /// **Validates: Requirements 1.6, 1.7, 2.1**
    ///
    /// For any list of NexusModsGame entries with distinct normalized names,
    /// building the lookup dictionary and then querying it with
    /// NormalizeName(entry.Name) for each entry returns the corresponding nexusmods_url.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildDictionary_ThenResolve_ReturnsCorrectUrl()
    {
        return Prop.ForAll(DistinctNexusModsGameListArb(), (List<NexusModsGame> entries) =>
        {
            // Arrange: create service with real NormalizeName
            var service = new NexusModsService(new HttpClient(), _gameDetection);

            // Act: build the dictionary
            service.BuildDictionary(entries);

            // Assert: every entry is retrievable by its normalized name
            foreach (var entry in entries)
            {
                var result = service.ResolveUrl(entry.Name, manifest: null);
                if (result != entry.NexusmodsUrl)
                {
                    return false.Label(
                        $"Expected URL '{entry.NexusmodsUrl}' for name '{entry.Name}' " +
                        $"(normalized: '{_gameDetection.NormalizeName(entry.Name)}'), " +
                        $"but got '{result ?? "(null)"}'");
                }
            }

            return true.Label("All entries resolved correctly");
        });
    }
}
