// Feature: nexus-pcgw-integration, Property 4: Steam AppID resolution priority chain
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for Steam AppID resolution priority chain.
/// For any game name, manifest steamAppIdOverrides, AppID cache, and DetectedGame.SteamAppId:
///   1. If the game name exists in steamAppIdOverrides, the override AppID is returned.
///   2. Else if the AppID cache contains the normalized game name, the cached AppID is returned.
///   3. Else if DetectedGame.SteamAppId is non-null, that AppID is returned.
///   Each step short-circuits (skips subsequent steps).
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
///
/// **Validates: Requirements 5.1, 5.2, 5a.3, 6.3**
/// </summary>
public class SteamAppIdPriorityPropertyTests
{
    private static readonly GameDetectionService _gameDetection = new();

    /// <summary>
    /// A non-existent install path to ensure steps 4-5 (file I/O, HTTP) don't interfere.
    /// </summary>
    private static readonly string _nonExistentPath =
        Path.Combine(Path.GetTempPath(), "SteamAppIdPriorityPropertyTests_nonexistent_" + Guid.NewGuid());

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
    /// Feature: nexus-pcgw-integration, Property 4: Steam AppID resolution priority chain
    ///
    /// **Validates: Requirements 5.1, 5a.3**
    ///
    /// When a game name exists in steamAppIdOverrides, the override AppID SHALL be returned
    /// regardless of cache and detected values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Override_AlwaysWins_OverCacheAndDetected()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from overrideId in PositiveAppIdGen()
            from cachedId in PositiveAppIdGen()
            from detectedId in PositiveAppIdGen()
            where overrideId != cachedId && overrideId != detectedId
            select (gameName, overrideId, cachedId, detectedId);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, overrideId, cachedId, detectedId) = tuple;

            var resolver = new SteamAppIdResolver(new HttpClient(), _gameDetection);

            var manifest = new RemoteManifest
            {
                SteamAppIdOverrides = new Dictionary<string, int>
                {
                    { gameName, overrideId }
                }
            };

            var normalized = _gameDetection.NormalizeName(gameName);
            var cache = new Dictionary<string, int> { { normalized, cachedId } };

            var result = resolver.ResolveAsync(gameName, detectedId, _nonExistentPath, manifest, cache)
                .GetAwaiter().GetResult();

            return (result == overrideId).Label(
                $"Expected override AppID {overrideId} but got {result?.ToString() ?? "(null)"} " +
                $"for game '{gameName}'");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 4: Steam AppID resolution priority chain
    ///
    /// **Validates: Requirements 6.3**
    ///
    /// When no override exists but the AppID cache contains the normalized game name,
    /// the cached AppID SHALL be returned regardless of detected value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Cache_WinsWhenNoOverride()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from cachedId in PositiveAppIdGen()
            from detectedId in PositiveAppIdGen()
            where cachedId != detectedId
            select (gameName, cachedId, detectedId);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, cachedId, detectedId) = tuple;

            var resolver = new SteamAppIdResolver(new HttpClient(), _gameDetection);

            // No manifest overrides
            var manifest = new RemoteManifest
            {
                SteamAppIdOverrides = new Dictionary<string, int>()
            };

            var normalized = _gameDetection.NormalizeName(gameName);
            var cache = new Dictionary<string, int> { { normalized, cachedId } };

            var result = resolver.ResolveAsync(gameName, detectedId, _nonExistentPath, manifest, cache)
                .GetAwaiter().GetResult();

            return (result == cachedId).Label(
                $"Expected cached AppID {cachedId} but got {result?.ToString() ?? "(null)"} " +
                $"for game '{gameName}'");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 4: Steam AppID resolution priority chain
    ///
    /// **Validates: Requirements 5.2**
    ///
    /// When no override and no cache entry exist but DetectedGame.SteamAppId is non-null,
    /// that AppID SHALL be returned.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Detected_WinsWhenNoOverrideAndNoCache()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from detectedId in PositiveAppIdGen()
            select (gameName, detectedId);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, detectedId) = tuple;

            var resolver = new SteamAppIdResolver(new HttpClient(), _gameDetection);

            // No manifest overrides
            var manifest = new RemoteManifest
            {
                SteamAppIdOverrides = new Dictionary<string, int>()
            };

            // Empty cache
            var cache = new Dictionary<string, int>();

            var result = resolver.ResolveAsync(gameName, detectedId, _nonExistentPath, manifest, cache)
                .GetAwaiter().GetResult();

            return (result == detectedId).Label(
                $"Expected detected AppID {detectedId} but got {result?.ToString() ?? "(null)"} " +
                $"for game '{gameName}'");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 4: Steam AppID resolution priority chain
    ///
    /// **Validates: Requirements 5a.3**
    ///
    /// When manifest is null (no overrides at all) and cache has an entry,
    /// the cached AppID SHALL be returned.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullManifest_FallsThroughToCache()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from cachedId in PositiveAppIdGen()
            select (gameName, cachedId);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, cachedId) = tuple;

            var resolver = new SteamAppIdResolver(new HttpClient(), _gameDetection);

            var normalized = _gameDetection.NormalizeName(gameName);
            var cache = new Dictionary<string, int> { { normalized, cachedId } };

            var result = resolver.ResolveAsync(gameName, null, _nonExistentPath, manifest: null, cache)
                .GetAwaiter().GetResult();

            return (result == cachedId).Label(
                $"Expected cached AppID {cachedId} but got {result?.ToString() ?? "(null)"} " +
                $"for game '{gameName}' with null manifest");
        });
    }
}
