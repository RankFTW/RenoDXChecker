using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using RenoDXChecker.Models;

namespace RenoDXChecker.Services;

public static class GameDetectionService
{
    // ── Steam ─────────────────────────────────────────────────────────────────────

    public static List<DetectedGame> FindSteamGames()
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
                    games.Add(new DetectedGame { Name = name, InstallPath = rootPath, Source = "Steam" });
                }
                catch { }
            }
        }
        return games;
    }

    // ── GOG ───────────────────────────────────────────────────────────────────────

    public static List<DetectedGame> FindGogGames()
    {
        var games = new List<DetectedGame>();
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Games")
                         ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
            if (key == null) return games;
            foreach (var sub in key.GetSubKeyNames())
            {
                using var gameKey = key.OpenSubKey(sub);
                if (gameKey == null) continue;
                var name = gameKey.GetValue("GAMENAME") as string ?? gameKey.GetValue("GameName") as string;
                var path = gameKey.GetValue("PATH") as string ?? gameKey.GetValue("path") as string;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path) && Directory.Exists(path))
                    games.Add(new DetectedGame { Name = name, InstallPath = path, Source = "GOG" });
            }
        }
        catch (SecurityException) { }
        return games;
    }

    // ── Epic ──────────────────────────────────────────────────────────────────────

    public static List<DetectedGame> FindEpicGames()
    {
        var games = new List<DetectedGame>();
        var manifestDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(manifestDir)) return games;
        foreach (var file in Directory.GetFiles(manifestDir, "*.item"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var name = ExtractJsonString(json, "DisplayName");
                var path = ExtractJsonString(json, "InstallLocation");
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path) && Directory.Exists(path))
                    games.Add(new DetectedGame { Name = name, InstallPath = path, Source = "Epic" });
            }
            catch { }
        }
        return games;
    }

    // ── EA App ────────────────────────────────────────────────────────────────────

    public static List<DetectedGame> FindEaGames()
    {
        var games = new List<DetectedGame>();
        var contentDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EA", "content");
        if (!Directory.Exists(contentDir)) return games;
        foreach (var dir in Directory.GetDirectories(contentDir))
        {
            var manifestFile = Path.Combine(dir, "__Installer", "installerdata.xml");
            if (!File.Exists(manifestFile)) continue;
            try
            {
                var xml  = File.ReadAllText(manifestFile);
                var name = Regex.Match(xml, @"<gameName>([^<]+)</gameName>").Groups[1].Value;
                var path = Regex.Match(xml, @"<defaultInstallPath>([^<]+)</defaultInstallPath>").Groups[1].Value;
                if (!string.IsNullOrEmpty(name) && Directory.Exists(path))
                    games.Add(new DetectedGame { Name = name, InstallPath = path, Source = "EA App" });
            }
            catch { }
        }
        return games;
    }

    // ── Engine + path detection ───────────────────────────────────────────────────

    public static (string installPath, EngineType engine) DetectEngineAndPath(string rootPath)
    {
        // --- Unreal Engine ---
        // Find Binaries\Win64 or Binaries\WinGDK that is NOT inside an Engine folder.
        // This is where ReShade and the .addon64 must be placed.
        var uePath = FindUEBinariesFolder(rootPath);
        if (uePath != null) return (uePath, EngineType.Unreal);

        // --- Unity ---
        // UnityPlayer.dll is always next to the exe (root or 1 level deep)
        var unityPlayer = FindFileShallow(rootPath, "UnityPlayer.dll", maxDepth: 2);
        if (unityPlayer != null)
            return (Path.GetDirectoryName(unityPlayer)!, EngineType.Unity);

        // --- Generic fallback ---
        var exeFolder = FindShallowExeFolder(rootPath);
        return (exeFolder ?? rootPath, EngineType.Unknown);
    }

    /// <summary>
    /// Finds the Binaries\Win64 or Binaries\WinGDK folder that is NOT inside
    /// an "Engine" directory. UE games have the structure:
    ///   GameRoot\
    ///     GameName\        ← or a codename folder
    ///       Binaries\
    ///         Win64\       ← THIS is where ReShade + .addon64 goes
    ///     Engine\          ← SKIP this entirely
    /// </summary>
    private static string? FindUEBinariesFolder(string root)
    {
        var candidates = new List<string>();
        CollectUEBinaries(root, 0, candidates);
        if (candidates.Count == 0) return null;

        // Prefer folders that contain a Shipping exe
        var withShipping = candidates.FirstOrDefault(c =>
            Directory.GetFiles(c, "*Shipping.exe").Length > 0 ||
            Directory.GetFiles(c, "*.exe").Any(f =>
                Path.GetFileName(f).Contains("Shipping", StringComparison.OrdinalIgnoreCase)));

        return withShipping ?? candidates[0];
    }

    private static void CollectUEBinaries(string dir, int depth, List<string> results)
    {
        if (depth > 5) return;
        try
        {
            foreach (var sub in Directory.GetDirectories(dir))
            {
                var name = Path.GetFileName(sub);

                // Skip Engine folder — its Binaries are for the engine, not the game
                if (name.Equals("Engine", StringComparison.OrdinalIgnoreCase)) continue;

                // Found a Binaries folder — look inside for Win64/WinGDK
                if (name.Equals("Binaries", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var binSub in Directory.GetDirectories(sub))
                    {
                        var binName = Path.GetFileName(binSub);
                        bool isTarget = binName.Equals("Win64",  StringComparison.OrdinalIgnoreCase)
                                     || binName.Equals("WinGDK", StringComparison.OrdinalIgnoreCase);
                        if (isTarget && Directory.GetFiles(binSub, "*.exe").Length > 0)
                            results.Add(binSub);
                    }
                    // Don't recurse further into Binaries
                    continue;
                }

                // Recurse into non-Engine, non-Binaries subfolders
                CollectUEBinaries(sub, depth + 1, results);
            }
        }
        catch { }
    }

    private static string? FindFileShallow(string dir, string pattern, int maxDepth)
    {
        if (maxDepth < 0 || !Directory.Exists(dir)) return null;
        try
        {
            var found = Directory.GetFiles(dir, pattern);
            if (found.Length > 0) return found[0];
            if (maxDepth > 0)
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var r = FindFileShallow(sub, pattern, maxDepth - 1);
                    if (r != null) return r;
                }
        }
        catch { }
        return null;
    }

    private static string? FindShallowExeFolder(string root)
    {
        var queue = new Queue<(string path, int depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var (dir, depth) = queue.Dequeue();
            if (depth > 4) continue;
            try
            {
                if (Directory.GetFiles(dir, "*.exe").Length > 0) return dir;
                foreach (var sub in Directory.GetDirectories(dir))
                    queue.Enqueue((sub, depth + 1));
            }
            catch { }
        }
        return null;
    }

    // ── Matching ──────────────────────────────────────────────────────────────────

    public static GameMod? MatchGame(DetectedGame game, IEnumerable<GameMod> mods)
    {
        var name = NormalizeName(game.Name);
        var modList = mods.Select(m => (mod: m, norm: NormalizeName(m.Name))).ToList();

        // 1. Exact normalised match
        var exact = modList.FirstOrDefault(t => t.norm == name);
        if (exact.mod != null) return exact.mod;

        // 2. Game name contains the mod name — e.g. detected "Code Vein GOTY" matches wiki "Code Vein"
        //    Pick the longest (most specific) mod name that fits inside the game name.
        //    This direction is safe: a longer detected name can match a shorter wiki entry.
        var containedBy = modList
            .Where(t => name.Contains(t.norm))
            .OrderByDescending(t => t.norm.Length)
            .FirstOrDefault();
        if (containedBy.mod != null) return containedBy.mod;

        // 3. Mod name contains the game name — e.g. abbreviated detected name
        //    ONLY when the extra characters in the mod name are NOT a sequel suffix (II, III, 2, 3…).
        //    This prevents "Code Vein" (game) from matching "Code Vein II" (mod).
        foreach (var t in modList.Where(t => t.norm.Contains(name)).OrderBy(t => t.norm.Length))
        {
            var suffix = t.norm.Substring(name.Length);
            // Reject if the suffix is a sequel indicator: roman numerals or digits
            var isSequel = System.Text.RegularExpressions.Regex.IsMatch(suffix, @"^[ivxlcdm0-9]+$");
            if (!isSequel) return t.mod;
        }

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static List<string> FindSteamLibraries(string steamPath)
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

    private static string? ExtractVdfValue(string content, string key) =>
        Regex.Match(content, $@"""{Regex.Escape(key)}""\s+""([^""]+)""", RegexOptions.IgnoreCase)
            is { Success: true } m ? m.Groups[1].Value : null;

    private static string? ExtractJsonString(string json, string key) =>
        Regex.Match(json, $@"""{Regex.Escape(key)}""\s*:\s*""([^""\\]*(\\.[^""\\]*)*)""")
            is { Success: true } m ? m.Groups[1].Value.Replace("\\\\", "\\").Replace("\\/", "/") : null;

    private static string? ReadRegistry(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath)
                         ?? Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue(valueName) as string;
        }
        catch { return null; }
    }

    public static string NormalizeName(string name) =>
        Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]", "");
}

public enum EngineType { Unknown, Unreal, Unity }
