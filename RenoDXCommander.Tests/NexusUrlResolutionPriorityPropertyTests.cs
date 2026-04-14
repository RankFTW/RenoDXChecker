// Feature: nexus-pcgw-integration, Property 2: Nexus URL resolution priority
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for Nexus URL resolution priority.
/// For any game name, manifest nexusUrlOverrides, and lookup dictionary:
///   1. If the game name exists in nexusUrlOverrides, the override URL is returned.
///   2. Else if NormalizeName(gameName) exists in the lookup dictionary, the dictionary URL is returned.
///   3. Else null is returned.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
///
/// **Validates: Requirements 2.3, 2.4, 2.5, 3.3**
/// </summary>
public class NexusUrlResolutionPriorityPropertyTests
{
    private static readonly GameDetectionService _gameDetection = new();

    /// <summary>
    /// Generates a non-empty alphanumeric game name that produces a non-empty normalized form.
    /// </summary>
    private static Gen<string> NonEmptyGameNameGen()
    {
        return Arb.Default.NonEmptyString().Generator
            .Select(s => s.Get)
            .Where(s => !string.IsNullOrEmpty(_gameDetection.NormalizeName(s)));
    }

    /// <summary>
    /// Generates a non-empty URL string.
    /// </summary>
    private static Gen<string> UrlGen(string prefix)
    {
        return Arb.Default.NonEmptyString().Generator
            .Select(s => prefix + s.Get);
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 2: Nexus URL resolution priority
    ///
    /// **Validates: Requirements 2.3, 2.4, 2.5, 3.3**
    ///
    /// When a game name exists in nexusUrlOverrides, the override URL SHALL be returned
    /// regardless of whether the game also exists in the dictionary.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Override_AlwaysWins_OverDictionary()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from overrideUrl in UrlGen("https://override.nexusmods.com/")
            from dictUrl in UrlGen("https://dict.nexusmods.com/")
            select (gameName, overrideUrl, dictUrl);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, overrideUrl, dictUrl) = tuple;

            var service = new NexusModsService(new HttpClient(), _gameDetection);

            // Build dictionary with an entry for this game
            var normalized = _gameDetection.NormalizeName(gameName);
            service.BuildDictionary(new List<NexusModsGame>
            {
                new() { Name = gameName, NexusmodsUrl = dictUrl }
            });

            // Create manifest with override for the same game
            var manifest = new RemoteManifest
            {
                NexusUrlOverrides = new Dictionary<string, string>
                {
                    { gameName, overrideUrl }
                }
            };

            var result = service.ResolveUrl(gameName, manifest);

            return (result == overrideUrl).Label(
                $"Expected override URL '{overrideUrl}' but got '{result ?? "(null)"}' " +
                $"for game '{gameName}'");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 2: Nexus URL resolution priority
    ///
    /// **Validates: Requirements 2.3, 2.4, 2.5, 3.3**
    ///
    /// When no override exists but the game's normalized name is in the dictionary,
    /// the dictionary URL SHALL be returned.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Dictionary_UsedWhenNoOverride()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from dictUrl in UrlGen("https://dict.nexusmods.com/")
            select (gameName, dictUrl);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, dictUrl) = tuple;

            var service = new NexusModsService(new HttpClient(), _gameDetection);

            // Build dictionary with an entry for this game
            service.BuildDictionary(new List<NexusModsGame>
            {
                new() { Name = gameName, NexusmodsUrl = dictUrl }
            });

            // No manifest overrides
            var manifest = new RemoteManifest
            {
                NexusUrlOverrides = new Dictionary<string, string>()
            };

            var result = service.ResolveUrl(gameName, manifest);

            return (result == dictUrl).Label(
                $"Expected dictionary URL '{dictUrl}' but got '{result ?? "(null)"}' " +
                $"for game '{gameName}'");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 2: Nexus URL resolution priority
    ///
    /// **Validates: Requirements 2.3, 2.4, 2.5, 3.3**
    ///
    /// When no override exists and the game is not in the dictionary,
    /// null SHALL be returned.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Null_WhenNoOverrideAndNoDictionary()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from otherName in NonEmptyGameNameGen()
            where _gameDetection.NormalizeName(gameName) != _gameDetection.NormalizeName(otherName)
            from dictUrl in UrlGen("https://dict.nexusmods.com/")
            select (gameName, otherName, dictUrl);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, otherName, dictUrl) = tuple;

            var service = new NexusModsService(new HttpClient(), _gameDetection);

            // Build dictionary with a DIFFERENT game
            service.BuildDictionary(new List<NexusModsGame>
            {
                new() { Name = otherName, NexusmodsUrl = dictUrl }
            });

            // No manifest overrides for this game
            var manifest = new RemoteManifest
            {
                NexusUrlOverrides = new Dictionary<string, string>()
            };

            var result = service.ResolveUrl(gameName, manifest);

            return (result == null).Label(
                $"Expected null but got '{result}' for game '{gameName}' " +
                $"(not in dictionary, no override)");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 2: Nexus URL resolution priority
    ///
    /// **Validates: Requirements 2.3, 2.4, 2.5, 3.3**
    ///
    /// When manifest is null (no overrides at all) and the game is in the dictionary,
    /// the dictionary URL SHALL be returned.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullManifest_FallsThroughToDictionary()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from dictUrl in UrlGen("https://dict.nexusmods.com/")
            select (gameName, dictUrl);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, dictUrl) = tuple;

            var service = new NexusModsService(new HttpClient(), _gameDetection);

            service.BuildDictionary(new List<NexusModsGame>
            {
                new() { Name = gameName, NexusmodsUrl = dictUrl }
            });

            // Pass null manifest
            var result = service.ResolveUrl(gameName, manifest: null);

            return (result == dictUrl).Label(
                $"Expected dictionary URL '{dictUrl}' but got '{result ?? "(null)"}' " +
                $"for game '{gameName}' with null manifest");
        });
    }
}
