using System.Text.Json.Serialization;

namespace RenoDXCommander.Models;

public class NexusModsGame
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("nexusmods_url")]
    public string NexusmodsUrl { get; set; } = "";
}
