using System.Text.Json;
using RenoDXChecker.Models;

namespace RenoDXChecker.Services;

public static class GameLibraryService
{
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXChecker", "game_library.json");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static SavedGameLibrary? Load()
    {
        try
        {
            if (!File.Exists(LibraryPath)) return null;
            return JsonSerializer.Deserialize<SavedGameLibrary>(File.ReadAllText(LibraryPath));
        }
        catch { return null; }
    }

    public static void Save(List<DetectedGame> games, Dictionary<string, bool> addonCache,
        HashSet<string> hiddenGames, List<DetectedGame> manualGames)
    {
        var lib = new SavedGameLibrary
        {
            LastScanned    = DateTime.UtcNow,
            AddonScanCache = addonCache,
            HiddenGames    = hiddenGames,
            Games = games.Select(g => new SavedGame
            {
                Name = g.Name, InstallPath = g.InstallPath, Source = g.Source
            }).ToList(),
            ManualGames = manualGames.Select(g => new SavedGame
            {
                Name = g.Name, InstallPath = g.InstallPath, Source = "Manual", IsManuallyAdded = true
            }).ToList(),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(lib, JsonOpts));
    }

    public static List<DetectedGame> ToDetectedGames(SavedGameLibrary lib) =>
        lib.Games.Select(g => new DetectedGame
        {
            Name = g.Name, InstallPath = g.InstallPath, Source = g.Source
        }).ToList();

    public static List<DetectedGame> ToManualGames(SavedGameLibrary lib) =>
        lib.ManualGames.Select(g => new DetectedGame
        {
            Name = g.Name, InstallPath = g.InstallPath, Source = "Manual", IsManuallyAdded = true
        }).ToList();
}
