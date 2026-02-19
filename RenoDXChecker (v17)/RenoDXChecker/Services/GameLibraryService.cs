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

    public static void Save(List<DetectedGame> games, Dictionary<string, bool> addonScanCache)
    {
        var lib = new SavedGameLibrary
        {
            LastScanned   = DateTime.UtcNow,
            AddonScanCache = addonScanCache,
            Games = games.Select(g => new SavedGame
            {
                Name        = g.Name,
                InstallPath = g.InstallPath,
                Source      = g.Source,
            }).ToList()
        };
        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(lib, JsonOpts));
    }

    public static List<DetectedGame> ToDetectedGames(SavedGameLibrary lib) =>
        lib.Games.Select(g => new DetectedGame
        {
            Name        = g.Name,
            InstallPath = g.InstallPath,
            Source      = g.Source,
        }).ToList();
}
