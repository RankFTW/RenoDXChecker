using HtmlAgilityPack;
using RenoDXChecker.Models;

namespace RenoDXChecker.Services;

public static class WikiService
{
    private const string WikiUrl = "https://github.com/clshortfuse/renodx/wiki/Mods";

    // All generic addons are hosted on clshortfuse.github.io/renodx/
    // Filenames confirmed from the wiki's generic mod section snapshot links
    public const string GenericUnrealUrl  = "https://clshortfuse.github.io/renodx/renodx-unrealengine.addon64";
    public const string GenericUnityUrl64 = "https://clshortfuse.github.io/renodx/renodx-unityengine.addon64";
    public const string GenericUnityUrl32 = "https://clshortfuse.github.io/renodx/renodx-unityengine.addon32";
    public const string GenericUnityUrl   = GenericUnityUrl64;

    public static async Task<(List<GameMod> Mods, Dictionary<string, string> GenericNotes)>
        FetchAllAsync(HttpClient http, IProgress<string>? progress = null)
    {
        progress?.Report("Fetching wiki...");
        var html = await http.GetStringAsync(WikiUrl);
        var doc  = new HtmlDocument();
        doc.LoadHtml(html);

        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables == null) return (new(), new());

        var mods         = ParseSpecificMods(tables[0]);
        var genericNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < tables.Count; i++)
            ParseGenericTable(tables[i], genericNotes);

        progress?.Report($"Found {mods.Count} specific mods, {genericNotes.Count} generic notes");
        return (mods, genericNotes);
    }

    private static List<GameMod> ParseSpecificMods(HtmlNode table)
    {
        var mods = new List<GameMod>();
        var rows = table.SelectNodes(".//tr")?.Skip(1);
        if (rows == null) return mods;

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");
            if (cells == null || cells.Count < 3) continue;

            var name       = Clean(cells[0].InnerText);
            var maintainer = cells.Count > 1 ? Clean(cells[1].InnerText) : "";
            var linksCell  = cells.Count > 2 ? cells[2] : null;
            var statusCell = cells.Count > 3 ? cells[3] : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            string? snapshotUrl = null, nexusUrl = null, discordUrl = null;
            if (linksCell != null)
            {
                foreach (var a in linksCell.SelectNodes(".//a") ?? Enumerable.Empty<HtmlNode>())
                {
                    var href = a.GetAttributeValue("href", "");
                    var text = a.InnerText.Trim().ToLowerInvariant();
                    if (href.Contains(".github.io/renodx/") || href.EndsWith(".addon64") || href.EndsWith(".addon32")
                        || href.Contains("/releases/download/"))
                        snapshotUrl = href;
                    else if (href.Contains("nexusmods.com"))
                        nexusUrl = href;
                    else if (href.Contains("discord.com") || href.Contains("discord.gg"))
                        discordUrl = href;
                    else if (text.Contains("snapshot") || text.Contains("download"))
                        snapshotUrl ??= href;
                }
            }

            string status = "✅";
            string? notes = null;
            if (statusCell != null)
            {
                status = statusCell.InnerText.Contains("🚧") ? "🚧" : "✅";
                foreach (var a in statusCell.SelectNodes(".//a[@title]") ?? Enumerable.Empty<HtmlNode>())
                {
                    var t = a.GetAttributeValue("title", "").Trim();
                    if (!string.IsNullOrEmpty(t)) { notes = t; break; }
                }
            }

            mods.Add(new GameMod
            {
                Name = name, Maintainer = maintainer,
                SnapshotUrl = snapshotUrl, NexusUrl = nexusUrl, DiscordUrl = discordUrl,
                Status = status, Notes = notes,
            });
        }
        return mods;
    }

    private static void ParseGenericTable(HtmlNode table, Dictionary<string, string> noteMap)
    {
        var rows = table.SelectNodes(".//tr")?.Skip(1);
        if (rows == null) return;

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");
            if (cells == null || cells.Count < 1) continue;

            var name = Clean(cells[0].InnerText);
            if (string.IsNullOrWhiteSpace(name)) continue;

            HtmlNode? notesCell = cells.Count > 2 ? cells[2]
                                : cells.Count > 1 ? cells[1]
                                : null;

            string? tooltipNote = null;
            if (cells.Count > 1)
            {
                foreach (var a in cells[1].SelectNodes(".//a[@title]") ?? Enumerable.Empty<HtmlNode>())
                {
                    tooltipNote = a.GetAttributeValue("title", "").Trim();
                    if (!string.IsNullOrEmpty(tooltipNote)) break;
                }
            }

            var noteText = notesCell != null ? BuildNoteText(notesCell) : "";
            if (string.IsNullOrEmpty(noteText) && !string.IsNullOrEmpty(tooltipNote))
                noteText = tooltipNote;

            if (!string.IsNullOrEmpty(noteText))
                noteMap[name] = noteText;
        }
    }

    private static string BuildNoteText(HtmlNode cell)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var node in cell.ChildNodes)
        {
            switch (node.NodeType)
            {
                case HtmlNodeType.Text:
                    var t = HtmlEntity.DeEntitize(node.InnerText);
                    t = System.Text.RegularExpressions.Regex.Replace(t, @"[ \t]+", " ");
                    sb.Append(t);
                    break;
                case HtmlNodeType.Element when node.Name == "br":
                    sb.Append('\n');
                    break;
                case HtmlNodeType.Element when node.Name is "code" or "kbd" or "samp":
                    var ct = HtmlEntity.DeEntitize(node.InnerText).Trim();
                    if (!string.IsNullOrEmpty(ct)) sb.Append($"`{ct}`");
                    break;
                case HtmlNodeType.Element:
                    var et = HtmlEntity.DeEntitize(node.InnerText).Trim();
                    if (!string.IsNullOrEmpty(et)) sb.Append(et);
                    break;
            }
        }
        var result = sb.ToString();
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\n{3,}", "\n\n");
        return result.Trim();
    }

    private static string Clean(string s) => HtmlEntity.DeEntitize(s ?? "").Trim();
}
