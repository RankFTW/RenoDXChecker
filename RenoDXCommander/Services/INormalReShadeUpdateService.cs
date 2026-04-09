namespace RenoDXCommander.Services;

/// <summary>
/// Downloads the latest ReShade without addon support and stages the DLLs
/// in the Normal_Staging directory (%LocalAppData%\RHI\reshade-normal\).
/// </summary>
public interface INormalReShadeUpdateService
{
    Task<(string version, string url)?> CheckLatestVersionAsync();

    Task<bool> EnsureLatestAsync(IProgress<(string msg, double pct)>? progress = null);

    static string? GetStagedVersion()
    {
        try
        {
            var versionFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RHI", "reshade-normal", "reshade_version.txt");
            return File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : null;
        }
        catch { return null; }
    }
}
