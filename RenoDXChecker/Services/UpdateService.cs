using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RenoDXCommander.Services;

/// <summary>
/// Checks GitHub Releases for a newer version of RDXC and downloads the installer if requested.
/// </summary>
public static class UpdateService
{
    // GitHub API endpoint for the "RDXC" release tag.
    // Using the tags endpoint gives a single release object directly.
    private const string ReleaseApiUrl =
        "https://api.github.com/repos/RankFTW/RenoDXChecker/releases/tags/RDXC";

    // The asset filename to download when updating.
    private const string InstallerFileName = "RDXC-Setup.exe";

    /// <summary>
    /// Returns the current app version from the assembly metadata (set via .csproj AssemblyVersion).
    /// </summary>
    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    /// <summary>
    /// Silently checks GitHub for a newer release. Returns the parsed remote version and
    /// the download URL for the installer asset, or null if no update is available or the
    /// check fails (network error, parse error, etc.).
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync(HttpClient http)
    {
        try
        {
            // GitHub API requires a User-Agent header.
            var request = new HttpRequestMessage(HttpMethod.Get, ReleaseApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("RDXC", CurrentVersion.ToString()));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var response = await http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                CrashReporter.Log($"UpdateService: GitHub API returned {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Parse version from tag_name or name — strip common prefixes like "v", "RDXC-", "RDXC "
            var tagName = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
            var releaseName = root.TryGetProperty("name", out var name) ? name.GetString() : null;

            var remoteVersion = ParseVersion(releaseName) ?? ParseVersion(tagName);
            if (remoteVersion == null)
            {
                CrashReporter.Log($"UpdateService: could not parse version from tag='{tagName}' name='{releaseName}'");
                return null;
            }

            // Compare — only offer update if remote is strictly newer
            var current = CurrentVersion;
            // Compare only Major.Minor.Build (ignore Revision which defaults to -1 or 0)
            var cmp = new Version(current.Major, current.Minor, current.Build);
            var rmt = new Version(remoteVersion.Major, remoteVersion.Minor, remoteVersion.Build);
            if (rmt <= cmp)
            {
                CrashReporter.Log($"UpdateService: up to date (local={cmp}, remote={rmt})");
                return null;
            }

            // Find the installer asset download URL
            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.Equals(assetName, InstallerFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var url)
                            ? url.GetString() : null;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                CrashReporter.Log($"UpdateService: update {rmt} found but no '{InstallerFileName}' asset in release");
                return null;
            }

            CrashReporter.Log($"UpdateService: update available {cmp} → {rmt}");
            return new UpdateInfo
            {
                CurrentVersion = cmp,
                RemoteVersion  = rmt,
                DownloadUrl    = downloadUrl,
            };
        }
        catch (Exception ex)
        {
            // Update check should never crash the app — swallow and log
            CrashReporter.Log($"UpdateService: check failed — {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads the installer to a temp file and returns the file path.
    /// Reports progress via the optional callback.
    /// </summary>
    public static async Task<string?> DownloadInstallerAsync(
        HttpClient http,
        string downloadUrl,
        IProgress<(string msg, double pct)>? progress = null)
    {
        try
        {
            progress?.Report(("Downloading update...", 0));

            var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("RDXC", CurrentVersion.ToString()));

            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var tempPath = Path.Combine(Path.GetTempPath(), InstallerFileName);

            await using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var pct = (double)totalRead / totalBytes * 100;
                    progress?.Report(($"Downloading update... {pct:F0}%", pct));
                }
            }

            progress?.Report(("Download complete.", 100));
            CrashReporter.Log($"UpdateService: downloaded installer to {tempPath} ({totalRead:N0} bytes)");
            return tempPath;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"UpdateService: download failed — {ex.Message}");
            progress?.Report(($"Download failed: {ex.Message}", 0));
            return null;
        }
    }

    /// <summary>
    /// Launches the downloaded installer and requests the app to close.
    /// </summary>
    public static void LaunchInstallerAndExit(string installerPath, Action closeApp)
    {
        try
        {
            CrashReporter.Log($"UpdateService: launching installer {installerPath}");
            Process.Start(new ProcessStartInfo
            {
                FileName        = installerPath,
                UseShellExecute = true,
            });

            // Give the installer a moment to start, then close RDXC
            closeApp();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"UpdateService: failed to launch installer — {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a version string from a release tag or name.
    /// Handles formats like: "1.2.2", "v1.2.2", "RDXC-1.2.2", "RDXC 1.2.2", "RDXC v1.2.2" etc.
    /// </summary>
    private static Version? ParseVersion(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        // Match the first occurrence of a version-like pattern (digits.digits.digits[.digits])
        var match = Regex.Match(input, @"(\d+\.\d+\.\d+(?:\.\d+)?)");
        if (!match.Success) return null;

        return Version.TryParse(match.Groups[1].Value, out var ver) ? ver : null;
    }
}

/// <summary>
/// Contains information about an available update.
/// </summary>
public class UpdateInfo
{
    public required Version CurrentVersion { get; init; }
    public required Version RemoteVersion  { get; init; }
    public required string  DownloadUrl    { get; init; }
}
