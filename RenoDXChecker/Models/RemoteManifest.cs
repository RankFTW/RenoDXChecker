using System.Text.Json.Serialization;

namespace RenoDXCommander.Models;

public class RemoteManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("wikiNameOverrides")]
    public Dictionary<string, string>? WikiNameOverrides { get; set; }

    [JsonPropertyName("ueExtendedGames")]
    public List<string>? UeExtendedGames { get; set; }

    [JsonPropertyName("nativeHdrGames")]
    public List<string>? NativeHdrGames { get; set; }

    [JsonPropertyName("blacklist")]
    public List<string>? Blacklist { get; set; }

    [JsonPropertyName("thirtyTwoBitGames")]
    public List<string>? ThirtyTwoBitGames { get; set; }

    [JsonPropertyName("sixtyFourBitGames")]
    public List<string>? SixtyFourBitGames { get; set; }

    [JsonPropertyName("gameNotes")]
    public Dictionary<string, GameNoteEntry>? GameNotes { get; set; }

    [JsonPropertyName("dcModeOverrides")]
    public Dictionary<string, int>? DcModeOverrides { get; set; }

    [JsonPropertyName("forceExternalOnly")]
    public Dictionary<string, ForceExternalEntry>? ForceExternalOnly { get; set; }

    [JsonPropertyName("installPathOverrides")]
    public Dictionary<string, string>? InstallPathOverrides { get; set; }

    [JsonPropertyName("wikiStatusOverrides")]
    public Dictionary<string, string>? WikiStatusOverrides { get; set; }

    /// <summary>
    /// Per-game snapshot URL overrides. When a game's matched mod has no SnapshotUrl
    /// (or the wiki parser fails to capture it), this provides a direct download URL.
    /// Key = game name, Value = direct addon download URL.
    /// </summary>
    [JsonPropertyName("snapshotOverrides")]
    public Dictionary<string, string>? SnapshotOverrides { get; set; }

    /// <summary>
    /// Games that should default to Luma mode when first detected.
    /// If the user has never toggled Luma for the game, it will be auto-enabled.
    /// </summary>
    [JsonPropertyName("lumaDefaultGames")]
    public List<string>? LumaDefaultGames { get; set; }

    /// <summary>
    /// Custom notes for games in Luma mode (shown in the info dialog when Luma is active).
    /// Supplements or replaces wiki-provided LumaMod notes.
    /// </summary>
    [JsonPropertyName("lumaGameNotes")]
    public Dictionary<string, GameNoteEntry>? LumaGameNotes { get; set; }

    /// <summary>
    /// Games in this list are unlinked from any fuzzy wiki match.
    /// They will fall through to the generic engine addon (Unreal or Unity)
    /// instead of being incorrectly associated with a named wiki mod.
    /// </summary>
    [JsonPropertyName("wikiUnlinks")]
    public List<string>? WikiUnlinks { get; set; }

    /// <summary>
    /// Per-game engine overrides. Allows the manifest to force a specific engine label
    /// for a game, overriding auto-detection.
    /// 
    /// Special values that affect filtering and mod behaviour:
    ///   "Unreal"         → treated as Unreal Engine 4/5 (filters into Unreal, eligible for UE-Extended)
    ///   "Unreal (Legacy)"→ treated as Unreal Engine 3 (filters into Unreal)
    ///   "Unity"          → treated as Unity (filters into Unity, eligible for generic Unity addon)
    /// 
    /// Any other string (e.g. "Silk", "Source 2", "Creation Engine") is stored as-is and
    /// displayed in the engine badge. The game filters into Other, not Unreal or Unity.
    /// Key = game name, Value = engine label string.
    /// </summary>
    [JsonPropertyName("engineOverrides")]
    public Dictionary<string, string>? EngineOverrides { get; set; }
}

public class GameNoteEntry
{
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("notesUrl")]
    public string? NotesUrl { get; set; }

    [JsonPropertyName("notesUrlLabel")]
    public string? NotesUrlLabel { get; set; }
}

public class ForceExternalEntry
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}
