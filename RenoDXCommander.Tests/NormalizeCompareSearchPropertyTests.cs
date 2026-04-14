// Feature: nexus-pcgw-integration, Property 5: Normalize-compare search result matching
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for normalize-compare search result matching.
/// For any game name and list of SteamStoreSearchItem results, the resolver SHALL return
/// the Id of the first item whose NormalizeName(item.Name) equals NormalizeName(gameName),
/// or null if no item matches.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
///
/// **Validates: Requirements 5.5, 5.6**
/// </summary>
public class NormalizeCompareSearchPropertyTests
{
    private static readonly GameDetectionService _gameDetection = new();

    /// <summary>
    /// Generates a non-empty game name that produces a non-empty normalized form.
    /// </summary>
    private static Gen<string> NonEmptyGameNameGen()
    {
        return Arb.Default.NonEmptyString().Generator
            .Select(s => s.Get)
            .Where(s => !string.IsNullOrEmpty(_gameDetection.NormalizeName(s)));
    }

    /// <summary>
    /// Generates a positive AppID (Steam AppIDs are positive integers).
    /// </summary>
    private static Gen<int> PositiveAppIdGen()
    {
        return Gen.Choose(1, int.MaxValue);
    }

    /// <summary>
    /// Generates a SteamStoreSearchItem with a non-empty name and positive Id.
    /// </summary>
    private static Gen<SteamStoreSearchItem> SearchItemGen()
    {
        return from name in Arb.Default.NonEmptyString().Generator.Select(s => s.Get)
               from id in PositiveAppIdGen()
               select new SteamStoreSearchItem { Name = name, Id = id };
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 5: Normalize-compare search result matching
    ///
    /// **Validates: Requirements 5.5, 5.6**
    ///
    /// When a result's normalized name matches the game's normalized name,
    /// FindMatchingAppId SHALL return that result's Id.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MatchingResult_ReturnsItsId()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from matchId in PositiveAppIdGen()
            from prefix in Gen.ListOf(SearchItemGen()).Select(l => l.ToList())
            let matchItem = new SteamStoreSearchItem { Name = gameName, Id = matchId }
            let results = prefix.Concat(new[] { matchItem }).ToList()
            select (gameName, matchId, results);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, matchId, results) = tuple;

            var resolver = new SteamAppIdResolver(new HttpClient(), _gameDetection);

            // The expected result is the Id of the FIRST item whose normalized name matches.
            var normalizedGame = _gameDetection.NormalizeName(gameName);
            var expectedFirst = results.First(r =>
                string.Equals(_gameDetection.NormalizeName(r.Name), normalizedGame, StringComparison.Ordinal));

            var result = resolver.FindMatchingAppId(gameName, results);

            return (result == expectedFirst.Id).Label(
                $"Expected AppID {expectedFirst.Id} but got {result?.ToString() ?? "(null)"} " +
                $"for game '{gameName}'");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 5: Normalize-compare search result matching
    ///
    /// **Validates: Requirements 5.5, 5.6**
    ///
    /// When multiple results match, FindMatchingAppId SHALL return the FIRST one's Id.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleMatches_ReturnsFirstId()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from firstId in PositiveAppIdGen()
            from secondId in PositiveAppIdGen()
            where firstId != secondId
            let results = new List<SteamStoreSearchItem>
            {
                new() { Name = gameName, Id = firstId },
                new() { Name = gameName, Id = secondId }
            }
            select (gameName, firstId, results);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, firstId, results) = tuple;

            var resolver = new SteamAppIdResolver(new HttpClient(), _gameDetection);
            var result = resolver.FindMatchingAppId(gameName, results);

            return (result == firstId).Label(
                $"Expected first matching AppID {firstId} but got {result?.ToString() ?? "(null)"} " +
                $"for game '{gameName}'");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 5: Normalize-compare search result matching
    ///
    /// **Validates: Requirements 5.5, 5.6**
    ///
    /// When no result's normalized name matches the game name,
    /// FindMatchingAppId SHALL return null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoMatch_ReturnsNull()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from items in Gen.ListOf(SearchItemGen()).Select(l => l.ToList())
            let normalizedGame = _gameDetection.NormalizeName(gameName)
            let nonMatching = items
                .Where(i => !string.Equals(_gameDetection.NormalizeName(i.Name), normalizedGame, StringComparison.Ordinal))
                .ToList()
            select (gameName, nonMatching);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, results) = tuple;

            var resolver = new SteamAppIdResolver(new HttpClient(), _gameDetection);
            var result = resolver.FindMatchingAppId(gameName, results);

            return (result == null).Label(
                $"Expected null but got {result?.ToString() ?? "(null)"} " +
                $"for game '{gameName}' with {results.Count} non-matching results");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 5: Normalize-compare search result matching
    ///
    /// **Validates: Requirements 5.5, 5.6**
    ///
    /// When the results list is empty, FindMatchingAppId SHALL return null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyResults_ReturnsNull()
    {
        return Prop.ForAll(NonEmptyGameNameGen().ToArbitrary(), gameName =>
        {
            var resolver = new SteamAppIdResolver(new HttpClient(), _gameDetection);
            var result = resolver.FindMatchingAppId(gameName, new List<SteamStoreSearchItem>());

            return (result == null).Label(
                $"Expected null for empty results but got {result?.ToString() ?? "(null)"} " +
                $"for game '{gameName}'");
        });
    }
}
