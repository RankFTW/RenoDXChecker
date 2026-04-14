// GameDetectionService.Steam.cs — Steam platform game detection
using System.Text.RegularExpressions;
using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

public partial class GameDetectionService
{
    // ── Steam ─────────────────────────────────────────────────────────────────────

    public List<DetectedGame> FindSteamGames()
    {
        var games = new List<DetectedGame>();
        var steamPath = ReadRegistry(@"SOFTWARE\Valve\Steam", "SteamPath")
                     ?? ReadRegistry(@"SOFTWARE\WOW6432Node\Valve\Steam", "SteamPath");
        if (steamPath == null) return games;
        steamPath = steamPath.Replace('/', '\\');

        foreach (var library in FindSteamLibraries(steamPath))
        {
            var steamapps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamapps)) continue;
            foreach (var acf in Directory.GetFiles(steamapps, "appmanifest_*.acf"))
            {
                try
                {
                    var content = File.ReadAllText(acf);
                    var name       = ExtractVdfValue(content, "name");
                    var installDir = ExtractVdfValue(content, "installdir");
                    if (name == null || installDir == null) continue;
                    var rootPath = Path.Combine(steamapps, "common", installDir);
                    if (!Directory.Exists(rootPath)) continue;
                    var game = new DetectedGame { Name = name, InstallPath = rootPath, Source = "Steam" };
                    var extractedAppId = ExtractAppIdFromAcfFilename(Path.GetFileName(acf));
                    if (extractedAppId.HasValue)
                        game.SteamAppId = extractedAppId.Value;
                    games.Add(game);
                }
                catch (Exception ex) { CrashReporter.Log($"[GameDetectionService.FindSteamGames] Failed to parse ACF '{acf}' — {ex.Message}"); }
            }
        }
        return games;
    }

    /// <summary>
    /// Extracts the numeric Steam AppID from an ACF filename like "appmanifest_601150.acf".
    /// Returns null if the filename does not match the expected pattern.
    /// </summary>
    internal static int? ExtractAppIdFromAcfFilename(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        if (name != null
            && name.StartsWith("appmanifest_")
            && int.TryParse(name["appmanifest_".Length..], out var appId))
        {
            return appId;
        }
        return null;
    }

    private List<string> FindSteamLibraries(string steamPath)
    {
        var libraries = new List<string> { steamPath };
        var vdfPath   = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath)) return libraries;
        foreach (Match m in Regex.Matches(File.ReadAllText(vdfPath), @"""path""\s+""([^""]+)"""))
        {
            var lib = m.Groups[1].Value.Replace(@"\\", @"\");
            if (Directory.Exists(lib) && !libraries.Contains(lib)) libraries.Add(lib);
        }
        return libraries;
    }
}
