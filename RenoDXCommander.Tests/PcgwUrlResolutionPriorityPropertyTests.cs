// Feature: nexus-pcgw-integration, Property 7: PCGW URL resolution priority
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for PCGW URL resolution priority.
/// For any game name, manifest pcgwUrlOverrides, and resolved Steam AppID:
///   1. If the game name exists in pcgwUrlOverrides, the override URL is returned.
///   2. Else if a Steam AppID is available, the appid.php URL is returned.
///   3. Else the OpenSearch fallback is attempted.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
///
/// **Validates: Requirements 7.1, 7a.3**
/// </summary>
public class PcgwUrlResolutionPriorityPropertyTests
{
    private static readonly GameDetectionService _gameDetection = new();

    /// <summary>
    /// A non-existent install path so file-based resolution steps are skipped.
    /// </summary>
    private static readonly string _nonExistentPath =
        Path.Combine(Path.GetTempPath(), "PcgwUrlResolutionPriorityPropertyTests_nonexistent_" + Guid.NewGuid());

    /// <summary>
    /// Stub ISteamAppIdResolver that always returns a predetermined AppID.
    /// </summary>
    private sealed class FixedAppIdResolver : ISteamAppIdResolver
    {
        private readonly int? _appId;

        public FixedAppIdResolver(int? appId) => _appId = appId;

        public Task<int?> ResolveAsync(
            string gameName, int? detectedAppId, string installPath,
            RemoteManifest? manifest, Dictionary<string, int>? appIdCache = null)
            => Task.FromResult(_appId);

        public int? FindMatchingAppId(string gameName, List<SteamStoreSearchItem> results) => null;
    }

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
    /// Generates a non-empty URL string with a given prefix.
    /// </summary>
    private static Gen<string> UrlGen(string prefix)
    {
        return Arb.Default.NonEmptyString().Generator
            .Select(s => prefix + s.Get);
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 7: PCGW URL resolution priority
    ///
    /// **Validates: Requirements 7.1, 7a.3**
    ///
    /// When a game name exists in pcgwUrlOverrides, the override URL SHALL be returned
    /// regardless of whether a Steam AppID is also available.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Override_AlwaysWins_OverAppId()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from overrideUrl in UrlGen("https://www.pcgamingwiki.com/wiki/Override_")
            from appId in PositiveAppIdGen()
            select (gameName, overrideUrl, appId);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, overrideUrl, appId) = tuple;

            // Even though the resolver would return an AppID, the override should win.
            var service = new PcgwService(
                new HttpClient(),
                new FixedAppIdResolver(appId),
                _gameDetection);

            var manifest = new RemoteManifest
            {
                PcgwUrlOverrides = new Dictionary<string, string>
                {
                    { gameName, overrideUrl }
                }
            };

            var result = service.ResolveUrlAsync(gameName, appId, _nonExistentPath, manifest)
                .GetAwaiter().GetResult();

            return (result == overrideUrl).Label(
                $"Expected override URL '{overrideUrl}' but got '{result ?? "(null)"}' " +
                $"for game '{gameName}' (AppID {appId} should be ignored)");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 7: PCGW URL resolution priority
    ///
    /// **Validates: Requirements 7.1, 7a.3**
    ///
    /// When no override exists but a Steam AppID is resolved, the appid.php URL SHALL be returned.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AppIdUrl_UsedWhenNoOverride()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from appId in PositiveAppIdGen()
            select (gameName, appId);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, appId) = tuple;

            var service = new PcgwService(
                new HttpClient(),
                new FixedAppIdResolver(appId),
                _gameDetection);

            // No pcgwUrlOverrides
            var manifest = new RemoteManifest
            {
                PcgwUrlOverrides = new Dictionary<string, string>()
            };

            var result = service.ResolveUrlAsync(gameName, appId, _nonExistentPath, manifest)
                .GetAwaiter().GetResult();

            var expected = $"https://www.pcgamingwiki.com/api/appid.php?appid={appId}";

            return (result == expected).Label(
                $"Expected appid.php URL '{expected}' but got '{result ?? "(null)"}' " +
                $"for game '{gameName}'");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 7: PCGW URL resolution priority
    ///
    /// **Validates: Requirements 7.1, 7a.3**
    ///
    /// When no override exists and no AppID is resolved, the OpenSearch fallback SHALL be attempted.
    /// We verify this by confirming the result is NOT an appid.php URL (it will be null or a wiki URL
    /// depending on the OpenSearch response, which we cannot control without HTTP mocking).
    /// With no real HTTP endpoint available, the fallback returns null for a non-existent game.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OpenSearchFallback_AttemptedWhenNoOverrideAndNoAppId()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            select gameName;

        return Prop.ForAll(gen.ToArbitrary(), gameName =>
        {
            // Resolver returns null (no AppID available)
            var service = new PcgwService(
                new HttpClient(),
                new FixedAppIdResolver(null),
                _gameDetection);

            // No pcgwUrlOverrides
            var manifest = new RemoteManifest
            {
                PcgwUrlOverrides = new Dictionary<string, string>()
            };

            var result = service.ResolveUrlAsync(gameName, null, _nonExistentPath, manifest)
                .GetAwaiter().GetResult();

            // The result should NOT be an appid.php URL (since no AppID was resolved).
            // It will be either null (OpenSearch failed/no results) or a wiki URL from OpenSearch.
            var isNotAppIdUrl = result == null || !result.Contains("appid.php");

            return isNotAppIdUrl.Label(
                $"Expected null or wiki URL (OpenSearch fallback) but got '{result ?? "(null)"}' " +
                $"for game '{gameName}' — should not be an appid.php URL when no AppID is resolved");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 7: PCGW URL resolution priority
    ///
    /// **Validates: Requirements 7.1, 7a.3**
    ///
    /// When manifest is null (no overrides at all) and a Steam AppID is resolved,
    /// the appid.php URL SHALL be returned.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullManifest_FallsThroughToAppIdUrl()
    {
        var gen =
            from gameName in NonEmptyGameNameGen()
            from appId in PositiveAppIdGen()
            select (gameName, appId);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, appId) = tuple;

            var service = new PcgwService(
                new HttpClient(),
                new FixedAppIdResolver(appId),
                _gameDetection);

            // Pass null manifest
            var result = service.ResolveUrlAsync(gameName, appId, _nonExistentPath, manifest: null)
                .GetAwaiter().GetResult();

            var expected = $"https://www.pcgamingwiki.com/api/appid.php?appid={appId}";

            return (result == expected).Label(
                $"Expected appid.php URL '{expected}' but got '{result ?? "(null)"}' " +
                $"for game '{gameName}' with null manifest");
        });
    }
}
