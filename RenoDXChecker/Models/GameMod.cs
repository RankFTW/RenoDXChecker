using System.Text.Json.Serialization;

namespace RenoDXChecker.Models;

public class GameMod
{
    public string Name { get; set; } = "";
    public string Maintainer { get; set; } = "";
    public string? SnapshotUrl { get; set; }      // 64-bit addon URL
    public string? SnapshotUrl32 { get; set; }    // 32-bit addon URL (Unity generic only)
    public string? NexusUrl { get; set; }
    public string? DiscordUrl { get; set; }
    public string Status { get; set; } = "✅";
    public string? StatusNote { get; set; }
    public string? Notes { get; set; }
    public bool IsGenericUnreal { get; set; }
    public bool IsGenericUnity { get; set; }

    [JsonIgnore]
    public string? AddonFileName => SnapshotUrl is not null ? Path.GetFileName(SnapshotUrl) : null;
    [JsonIgnore]
    public string? AddonFileName32 => SnapshotUrl32 is not null ? Path.GetFileName(SnapshotUrl32) : null;
    [JsonIgnore]
    public bool Is32Bit => SnapshotUrl?.EndsWith(".addon32") == true;
    [JsonIgnore]
    public bool HasBothBitVersions => SnapshotUrl != null && SnapshotUrl32 != null;
}

public class InstalledModRecord
{
    public string GameName { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string AddonFileName { get; set; } = "";
    public string? FileHash { get; set; }
    public DateTime InstalledAt { get; set; }
    public string? SnapshotUrl { get; set; }
}

public class DetectedGame
{
    public string Name { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string Source { get; set; } = "";
    public GameMod? MatchedMod { get; set; }
    public InstalledModRecord? InstalledRecord { get; set; }
    public bool UpdateAvailable { get; set; }
}

public class SavedGameLibrary
{
    public DateTime LastScanned { get; set; }
    public List<SavedGame> Games { get; set; } = new();
    /// <summary>Key = installPath, Value = addonFileName found (or null if none)</summary>
    public Dictionary<string, bool> AddonScanCache { get; set; } = new();
}

public class SavedGame
{
    public string Name { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string Source { get; set; } = "";
}
