using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>
/// Installs and manages RE Framework (dinput8.dll) for RE Engine games.
/// </summary>
public interface IREFrameworkService
{
    /// <summary>
    /// Downloads MHWILDS.zip from the latest nightly release, extracts dinput8.dll,
    /// caches it, and copies it to the game's install path.
    /// </summary>
    Task<REFrameworkInstalledRecord> InstallAsync(
        string gameName, string installPath,
        IProgress<(string message, double percent)>? progress = null);

    /// <summary>Deletes dinput8.dll from the game directory and removes the install record.</summary>
    void Uninstall(string gameName, string installPath);

    /// <summary>Checks GitHub nightly releases for a newer version than the installed one.</summary>
    Task<bool> CheckForUpdateAsync(string installedVersion);

    /// <summary>Returns the latest release tag from the nightly API (cached per session).</summary>
    Task<string?> GetLatestVersionAsync();

    /// <summary>Loads all persisted install records from disk.</summary>
    List<REFrameworkInstalledRecord> GetRecords();
}
