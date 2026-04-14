using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>
/// Fetches, caches, and queries the Nexus Mods game catalogue
/// to resolve per-game Nexus Mods page URLs.
/// </summary>
public interface INexusModsService
{
    /// <summary>
    /// Fetches games.json (or loads from cache), builds the lookup dictionary.
    /// Called once at startup.
    /// </summary>
    Task InitAsync();

    /// <summary>
    /// Returns the Nexus Mods URL for a game, checking:
    /// 1. Manifest nexusUrlOverrides (highest priority)
    /// 2. Normalized-name dictionary lookup
    /// Returns null if no match.
    /// </summary>
    string? ResolveUrl(string gameName, RemoteManifest? manifest);
}
