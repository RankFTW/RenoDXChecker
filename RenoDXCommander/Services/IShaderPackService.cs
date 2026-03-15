namespace RenoDXCommander.Services;

/// <summary>
/// Downloads, extracts, and deploys HDR ReShade shader packs.
/// </summary>
public interface IShaderPackService
{
    Task EnsureLatestAsync(IProgress<string>? progress = null);

    void DeployToDcFolder(ShaderPackService.DeployMode? mode = null);

    void DeployToGameFolder(string gameDir, ShaderPackService.DeployMode? mode = null);

    void RemoveFromGameFolder(string gameDir);

    bool IsManagedByRdxc(string gameDir);

    void RestoreOriginalIfPresent(string gameDir);

    void SyncDcFolder(ShaderPackService.DeployMode m);

    void SyncGameFolder(string gameDir, ShaderPackService.DeployMode m);

    void SyncShadersToAllLocations(
        IEnumerable<(string installPath, bool dcInstalled, bool rsInstalled, bool dcMode, string? shaderModeOverride)> locations,
        ShaderPackService.DeployMode? mode = null);
}
