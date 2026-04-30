namespace RenoDXCommander.Services;

/// <summary>
/// Fetches Lyall's ultrawide fix repos from Codeberg, caches them to disk
/// with a 24-hour TTL, and resolves per-game fix URLs.
/// </summary>
public interface ILyallFixService
{
    /// <summary>
    /// Fetches the repo list from Codeberg (or loads from cache), builds the lookup dictionary.
    /// Called once at startup.
    /// </summary>
    Task InitAsync();

    /// <summary>
    /// Returns the Codeberg repo URL for a game's ultrawide fix, or null if none exists.
    /// Checks manifest overrides first, then the normalized-name dictionary.
    /// </summary>
    string? ResolveUrl(string gameName);
}
