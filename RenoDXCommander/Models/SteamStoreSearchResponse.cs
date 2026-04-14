using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RenoDXCommander.Models;

public class SteamStoreSearchResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<SteamStoreSearchItem> Items { get; set; } = new();
}

public class SteamStoreSearchItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
