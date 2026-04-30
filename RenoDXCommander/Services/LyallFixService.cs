using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RenoDXCommander.Services;

/// <summary>
/// Fetches Lyall's ultrawide fix repos from Codeberg, caches them to disk
/// with a 24-hour TTL, builds a normalized-name lookup dictionary, and resolves per-game URLs.
/// </summary>
public partial class LyallFixService : ILyallFixService
{
    private readonly HttpClient _http;
    private readonly IGameDetectionService _gameDetection;

    private const string ReposApiUrl = "https://codeberg.org/api/v1/users/Lyall/repos?limit=50";
    private const string SkipRepoName = "UltrawidePatches";

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RHI", "lyall_repos.json");

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    /// <summary>Normalized game name → html_url.</summary>
    private Dictionary<string, string> _lookup = new(StringComparer.Ordinal);

    [GeneratedRegex(@"(?:An ASI plugin|A fix|A BepInEx plugin) for (.+?) that ", RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionGameNameRegex();

    [GeneratedRegex(@"\s*\(and [^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ParentheticalAndRegex();

    public LyallFixService(HttpClient http, IGameDetectionService gameDetection)
    {
        _http = http;
        _gameDetection = gameDetection;
    }

    /// <inheritdoc />
    public async Task InitAsync()
    {
        string? json = null;

        // Skip fetch if the cache file is fresh (< 24 hours old).
        bool cacheFresh = false;
        try
        {
            if (File.Exists(CachePath))
            {
                var lastWrite = File.GetLastWriteTimeUtc(CachePath);
                cacheFresh = (DateTime.UtcNow - lastWrite) < CacheTtl;
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[LyallFixService.InitAsync] Cache freshness check failed — {ex.Message}");
        }

        if (cacheFresh)
        {
            json = LoadCacheFromDisk();
            if (json != null)
            {
                CrashReporter.Log("[LyallFixService.InitAsync] Cache is fresh, skipping fetch");
            }
        }

        // Fetch from network if cache was not fresh or unreadable.
        if (json == null)
        {
            try
            {
                json = await _http.GetStringAsync(ReposApiUrl).ConfigureAwait(false);
                CrashReporter.Log("[LyallFixService.InitAsync] Fetched repos from Codeberg");

                // Persist to disk cache.
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
                    await File.WriteAllTextAsync(CachePath, json).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[LyallFixService.InitAsync] Cache write failed — {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[LyallFixService.InitAsync] Fetch failed — {ex.Message}");

                // Fallback to disk cache.
                json = LoadCacheFromDisk();
                if (json == null)
                {
                    CrashReporter.Log("[LyallFixService.InitAsync] No cache available — Lyall fix resolution disabled");
                    return;
                }
            }
        }

        // Parse and build the lookup dictionary.
        try
        {
            var repos = JsonSerializer.Deserialize<List<CodebergRepo>>(json);
            if (repos != null)
                BuildDictionary(repos);

            CrashReporter.Log($"[LyallFixService.InitAsync] Built dictionary with {_lookup.Count} entries");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[LyallFixService.InitAsync] JSON parse failed — {ex.Message}");

            if (!cacheFresh)
            {
                var fallback = LoadCacheFromDisk();
                if (fallback != null)
                {
                    try
                    {
                        var repos = JsonSerializer.Deserialize<List<CodebergRepo>>(fallback);
                        if (repos != null)
                            BuildDictionary(repos);
                    }
                    catch (Exception innerEx)
                    {
                        CrashReporter.Log($"[LyallFixService.InitAsync] Cache JSON also invalid — {innerEx.Message}");
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public string? ResolveUrl(string gameName)
    {
        try
        {
            // Dictionary lookup by normalized name.
            var normalized = _gameDetection.NormalizeName(gameName);
            if (!string.IsNullOrEmpty(normalized)
                && _lookup.TryGetValue(normalized, out var url))
            {
                return url;
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[LyallFixService.ResolveUrl] Failed for '{gameName}' — {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Builds the normalized-name → URL lookup dictionary from parsed Codeberg repos.
    /// Only includes repos with releases and a parseable game name in the description.
    /// </summary>
    internal void BuildDictionary(List<CodebergRepo> repos)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var regex = DescriptionGameNameRegex();
        var parenRegex = ParentheticalAndRegex();

        foreach (var repo in repos)
        {
            // Skip the collection repo
            if (string.Equals(repo.Name, SkipRepoName, StringComparison.OrdinalIgnoreCase))
                continue;

            // Only include repos that have releases
            if (!repo.HasReleases)
                continue;

            if (string.IsNullOrEmpty(repo.Description) || string.IsNullOrEmpty(repo.HtmlUrl))
                continue;

            // Extract game name from description
            var match = regex.Match(repo.Description);
            if (!match.Success)
                continue;

            var gameName = match.Groups[1].Value.Trim();

            // Strip "(and ...)" parenthetical
            gameName = parenRegex.Replace(gameName, "").Trim();

            if (string.IsNullOrEmpty(gameName))
                continue;

            // Strip bold markdown markers if present
            gameName = gameName.Replace("**", "");

            if (string.IsNullOrEmpty(gameName))
                continue;

            var key = _gameDetection.NormalizeName(gameName);
            if (!string.IsNullOrEmpty(key))
                dict.TryAdd(key, repo.HtmlUrl);
        }

        _lookup = dict;
    }

    /// <summary>
    /// Reads the cache file from disk. Returns null if the file doesn't exist or is unreadable.
    /// </summary>
    private static string? LoadCacheFromDisk()
    {
        try
        {
            if (File.Exists(CachePath))
                return File.ReadAllText(CachePath);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[LyallFixService.LoadCacheFromDisk] Read failed — {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Minimal model for Codeberg API repo response.
    /// </summary>
    internal class CodebergRepo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("has_releases")]
        public bool HasReleases { get; set; }
    }
}
