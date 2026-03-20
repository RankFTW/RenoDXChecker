using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>
/// Installs and manages Display Commander and ReShade for each game.
/// </summary>
public interface IAuxInstallService
{
    Task<AuxInstalledRecord> InstallDcAsync(
        string gameName,
        string installPath,
        string? dllFileName,
        AuxInstalledRecord? existingDcRecord = null,
        AuxInstalledRecord? existingRsRecord = null,
        string? shaderModeOverride = null,
        bool use32Bit = false,
        string? filenameOverride = null,
        IEnumerable<string>? selectedPackIds = null,
        IProgress<(string message, double percent)>? progress = null);

    Task<AuxInstalledRecord> InstallReShadeAsync(
        string gameName,
        string installPath,
        bool dcMode,
        bool dcIsInstalled = false,
        string? shaderModeOverride = null,
        bool use32Bit = false,
        string? filenameOverride = null,
        IEnumerable<string>? selectedPackIds = null,
        IProgress<(string message, double percent)>? progress = null);

    Task<bool> CheckForUpdateAsync(AuxInstalledRecord record);

    void Uninstall(AuxInstalledRecord record);

    /// <summary>
    /// Removes only the DLL file and DB record for the given install, without
    /// triggering shader folder operations (RestoreOriginalIfPresent).
    /// Used by DC mode switching where shaders must remain untouched.
    /// </summary>
    void UninstallDllOnly(AuxInstalledRecord record);

    List<AuxInstalledRecord> LoadAll();

    AuxInstalledRecord? FindRecord(string gameName, string installPath, string addonType);

    void SaveAuxRecord(AuxInstalledRecord record);

    void RemoveRecord(AuxInstalledRecord record);
}
