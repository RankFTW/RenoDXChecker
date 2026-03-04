using System.Text.Json;
using System.Text.Json.Serialization;
using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>
/// Fetches, caches, and provides the remote game manifest.
/// The manifest is a single JSON file hosted on GitHub that provides
/// centralized game configuration (wiki name overrides, UE-Extended flags,
/// native HDR games, notes, blacklist, etc.).
/// </summary>
public static class ManifestService
{
    /// <summary>GitHub API endpoint — no CDN cache delay, reflects commits instantly.</summary>
    private const string GitHubApiUrl =
        "https://api.github.com/repos/RankFTW/rdxc-manifest/contents/manifest.json";

    /// <summary>Fallback raw URL — uses GitHub CDN (may be up to ~5 min behind HEAD).</summary>
    private const string RawFallbackUrl =
        "https://raw.githubusercontent.com/RankFTW/rdxc-manifest/main/manifest.json";

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXCommander", "manifest.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Fetches the manifest from the GitHub API (instant updates). Falls back
    /// to the raw CDN URL, then to the local cache.
    /// Returns null if all sources are unavailable.
    /// </summary>
    public static async Task<RemoteManifest?> FetchAsync(HttpClient http)
    {
        // Try GitHub API first (no CDN delay)
        string? json = null;
        try
        {
            json = await FetchViaGitHubApiAsync(http);
            CrashReporter.Log("Manifest fetched via GitHub API");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"Manifest GitHub API failed ({ex.Message}), trying raw URL...");
        }

        // Fallback to raw.githubusercontent.com
        if (json == null)
        {
            try
            {
                json = await http.GetStringAsync(RawFallbackUrl);
                CrashReporter.Log("Manifest fetched via raw fallback URL");
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"Manifest raw fetch also failed ({ex.Message}), trying cache...");
            }
        }

        if (json == null)
            return LoadCached();

        try
        {
            var manifest = JsonSerializer.Deserialize<RemoteManifest>(json, JsonOptions);
            if (manifest != null)
            {
                Normalize(manifest);

                // Cache for offline use
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
                    await File.WriteAllTextAsync(CachePath, json);
                }
                catch { /* cache write failure is non-fatal */ }
            }
            CrashReporter.Log($"Manifest fetched: v{manifest?.Version}, " +
                $"{manifest?.WikiNameOverrides?.Count ?? 0} name overrides, " +
                $"{manifest?.GameNotes?.Count ?? 0} game notes");
            return manifest;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"Manifest deserialization failed ({ex.Message}), trying cache...");
            return LoadCached();
        }
    }

    /// <summary>
    /// Fetches manifest JSON via the GitHub Contents API which returns
    /// base64-encoded file content with no CDN caching delay.
    /// </summary>
    private static async Task<string> FetchViaGitHubApiAsync(HttpClient http)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
        request.Headers.Add("Accept", "application/vnd.github.v3+json");
        // User-Agent is already set on the shared HttpClient

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var apiJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(apiJson);
        var root = doc.RootElement;

        var contentBase64 = root.GetProperty("content").GetString()
            ?? throw new InvalidOperationException("GitHub API response missing 'content' field");

        // GitHub returns base64 with embedded newlines — strip them before decoding
        contentBase64 = contentBase64.Replace("\n", "").Replace("\r", "");
        var bytes = Convert.FromBase64String(contentBase64);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Loads the manifest from the local cache file.
    /// Returns null if no cache exists or the file is corrupt.
    /// </summary>
    public static RemoteManifest? LoadCached()
    {
        try
        {
            if (!File.Exists(CachePath)) return null;
            var json = File.ReadAllText(CachePath);
            var manifest = JsonSerializer.Deserialize<RemoteManifest>(json, JsonOptions);
            if (manifest != null)
                Normalize(manifest);
            CrashReporter.Log($"Manifest loaded from cache: v{manifest?.Version}");
            return manifest;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"Manifest cache load failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Rebuilds dictionary properties with case-insensitive string comparers
    /// so game-name lookups are forgiving of casing differences.
    /// </summary>
    private static void Normalize(RemoteManifest m)
    {
        if (m.WikiNameOverrides != null)
            m.WikiNameOverrides = new Dictionary<string, string>(m.WikiNameOverrides, StringComparer.OrdinalIgnoreCase);
        if (m.GameNotes != null)
            m.GameNotes = new Dictionary<string, GameNoteEntry>(m.GameNotes, StringComparer.OrdinalIgnoreCase);
        if (m.DcModeOverrides != null)
            m.DcModeOverrides = new Dictionary<string, int>(m.DcModeOverrides, StringComparer.OrdinalIgnoreCase);
        if (m.ForceExternalOnly != null)
            m.ForceExternalOnly = new Dictionary<string, ForceExternalEntry>(m.ForceExternalOnly, StringComparer.OrdinalIgnoreCase);
        if (m.InstallPathOverrides != null)
            m.InstallPathOverrides = new Dictionary<string, string>(m.InstallPathOverrides, StringComparer.OrdinalIgnoreCase);
        if (m.WikiStatusOverrides != null)
            m.WikiStatusOverrides = new Dictionary<string, string>(m.WikiStatusOverrides, StringComparer.OrdinalIgnoreCase);
        if (m.SnapshotOverrides != null)
            m.SnapshotOverrides = new Dictionary<string, string>(m.SnapshotOverrides, StringComparer.OrdinalIgnoreCase);
        if (m.LumaGameNotes != null)
            m.LumaGameNotes = new Dictionary<string, GameNoteEntry>(m.LumaGameNotes, StringComparer.OrdinalIgnoreCase);
    }
}
