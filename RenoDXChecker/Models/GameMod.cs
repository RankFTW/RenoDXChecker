using System.Text.Json.Serialization;

namespace RenoDXChecker.Models;

public class GameMod
{
    public string Name { get; set; } = "";
    public string Maintainer { get; set; } = "";
    public string? SnapshotUrl { get; set; }
    public string? SnapshotUrl32 { get; set; }
    public string? NexusUrl { get; set; }
    public string? DiscordUrl { get; set; }
    public string Status { get; set; } = "✅";
    public string? Notes { get; set; }
    public bool IsGenericUnreal { get; set; }
    public bool IsGenericUnity { get; set; }
    // Optional URL found in the game name cell on the wiki (points to per-game instructions)
    public string? NameUrl { get; set; }

    [JsonIgnore] public string? AddonFileName   => SnapshotUrl   != null ? Path.GetFileName(SnapshotUrl)   : null;
    [JsonIgnore] public string? AddonFileName32  => SnapshotUrl32 != null ? Path.GetFileName(SnapshotUrl32) : null;
    [JsonIgnore] public bool HasBothBitVersions  => SnapshotUrl != null && SnapshotUrl32 != null;
}

public class InstalledModRecord
{
    public string GameName { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string AddonFileName { get; set; } = "";
    public string? FileHash { get; set; }
    public DateTime InstalledAt { get; set; }
    public string? SnapshotUrl { get; set; }
    /// <summary>Last-Modified header from the snapshot at time of last check.</summary>
    public DateTime? SnapshotLastModified { get; set; }
}

public class DetectedGame
{
    public string Name { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string Source { get; set; } = "";
    public bool IsManuallyAdded { get; set; }
}

public class SavedGameLibrary
{
    public DateTime LastScanned { get; set; }
    public List<SavedGame> Games { get; set; } = new();
    public Dictionary<string, bool> AddonScanCache { get; set; } = new();
    public HashSet<string> HiddenGames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<SavedGame> ManualGames { get; set; } = new();
}

public class SavedGame
{
    public string Name { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string Source { get; set; } = "";
    public bool IsManuallyAdded { get; set; }
}
