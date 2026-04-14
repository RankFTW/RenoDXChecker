using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>
/// Resolves Steam AppIDs for games through a multi-step priority chain.
/// </summary>
public interface ISteamAppIdResolver
{
    /// <summary>
    /// Resolves a Steam AppID for a game through the priority chain:
    /// 1. Manifest steamAppIdOverrides (highest priority)
    /// 2. Cached AppID (from the AppID cache dictionary)
    /// 3. DetectedGame.SteamAppId (from ACF parsing)
    /// 4. steam_appid.txt in install directory
    /// 5. Steam Store search API (rate-limited to 1 req/sec)
    /// Returns null if unresolvable.
    /// </summary>
    Task<int?> ResolveAsync(
        string gameName,
        int? detectedAppId,
        string installPath,
        RemoteManifest? manifest,
        Dictionary<string, int>? appIdCache = null);

    /// <summary>
    /// Finds the first Steam Store search result whose normalized name matches the game name.
    /// Returns the AppID of the matching result, or null if no match.
    /// Exposed for testability (Property 5: Normalize-compare search result matching).
    /// </summary>
    int? FindMatchingAppId(string gameName, List<SteamStoreSearchItem> results);
}
