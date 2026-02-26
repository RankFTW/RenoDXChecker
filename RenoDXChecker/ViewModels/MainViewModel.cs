using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using ShaderDeployMode = RenoDXCommander.Services.ShaderPackService.DeployMode;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace RenoDXCommander.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly HttpClient        _http;
    public HttpClient HttpClient => _http;
    private readonly ModInstallService _installer;
    private readonly AuxInstallService _auxInstaller;
    [ObservableProperty] private bool _dcModeEnabled;
    [ObservableProperty] private ShaderDeployMode _shaderDeployMode = ShaderDeployMode.Minimum;

    /// <summary>
    /// Raised when an install would overwrite a dxgi.dll that RDXC cannot identify
    /// as ReShade or Display Commander. The UI should show a confirmation dialog
    /// <summary>
    /// Async callback set by the UI layer. Called when a foreign dxgi.dll is detected.
    /// Returns true if the user confirms overwrite, false to cancel.
    /// </summary>
    public Func<GameCardViewModel, string, Task<bool>>? ConfirmForeignDxgiOverwrite { get; set; }

    partial void OnShaderDeployModeChanged(ShaderDeployMode v)
    {
        ShaderPackService.CurrentMode = v;
        SaveNameMappings();
        // Notify computed button properties
        OnPropertyChanged(nameof(ShadersBtnLabel));
        OnPropertyChanged(nameof(ShadersBtnBackground));
        OnPropertyChanged(nameof(ShadersBtnForeground));
        OnPropertyChanged(nameof(ShadersBtnBorder));
    }

    /// <summary>Cycles Off → Minimum → All → Off and returns new mode.</summary>
    public ShaderDeployMode CycleShaderDeployMode()
    {
        ShaderDeployMode = ShaderDeployMode switch
        {
            ShaderDeployMode.Off     => ShaderDeployMode.Minimum,
            ShaderDeployMode.Minimum => ShaderDeployMode.All,
            ShaderDeployMode.All     => ShaderDeployMode.User,
            _                        => ShaderDeployMode.Off,
        };
        return ShaderDeployMode;
    }

    // Shaders button label / colours — shown in the header bar
    public string ShadersBtnLabel => ShaderDeployMode switch
    {
        ShaderDeployMode.Off     => "🎨 Shaders: Off",
        ShaderDeployMode.Minimum => "🎨 Shaders: Minimum",
        ShaderDeployMode.All     => "🎨 Shaders: All",
        ShaderDeployMode.User    => "🎨 Shaders: User",
        _                        => "🎨 Shaders",
    };
    public string ShadersBtnBackground => ShaderDeployMode switch
    {
        ShaderDeployMode.Off     => "#1A1820",
        ShaderDeployMode.Minimum => "#1A1A30",
        ShaderDeployMode.All     => "#1A1040",
        ShaderDeployMode.User    => "#0E1E20",
        _                        => "#1A1820",
    };
    public string ShadersBtnForeground => ShaderDeployMode switch
    {
        ShaderDeployMode.Off     => "#555066",
        ShaderDeployMode.Minimum => "#8878C8",
        ShaderDeployMode.All     => "#C090FF",
        ShaderDeployMode.User    => "#40C0B0",
        _                        => "#555066",
    };
    public string ShadersBtnBorder => ShaderDeployMode switch
    {
        ShaderDeployMode.Off     => "#2A2535",
        ShaderDeployMode.Minimum => "#3A2880",
        ShaderDeployMode.All     => "#6030B0",
        ShaderDeployMode.User    => "#1A5050",
        _                        => "#2A2535",
    };

    /// <summary>The current ShaderDeployMode as a string for AuxInstallService calls.</summary>
    public ShaderDeployMode CurrentShaderMode => ShaderDeployMode;
    private List<GameMod> _allMods = new();
    private Dictionary<string, string> _genericNotes = new(StringComparer.OrdinalIgnoreCase);
    private List<GameCardViewModel> _allCards = new();
    public IReadOnlyList<GameCardViewModel> AllCards => _allCards;
    private List<DetectedGame> _manualGames = new();
    private HashSet<string> _hiddenGames = new(StringComparer.OrdinalIgnoreCase);

    // Settings stored as JSON — ApplicationData.Current throws in unpackaged WinUI 3
    private static readonly string _settingsFilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXCommander", "settings.json");

    private static Dictionary<string, string> LoadSettingsFile()
    {
        try
        {
            if (!System.IO.File.Exists(_settingsFilePath)) return new(StringComparer.OrdinalIgnoreCase);
            var json = System.IO.File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch { return new(StringComparer.OrdinalIgnoreCase); }
    }

    private static void SaveSettingsFile(Dictionary<string, string> settings)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_settingsFilePath)!);
            System.IO.File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(settings));
        }
        catch { }
    }

    [ObservableProperty] private string _statusText = "Loading...";
    [ObservableProperty] private string _subStatusText = "";
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private string _filterMode = "Detected";
    [ObservableProperty] private bool _showHidden = false;
    [ObservableProperty] private int _totalGames;
    [ObservableProperty] private int _installedCount;
    [ObservableProperty] private int _hiddenCount;

    public ObservableCollection<GameCardViewModel> DisplayedGames { get; } = new();

    // UE common warnings shown at bottom of every generic UE info dialog
    private const string UnrealWarnings =
        "\n\n⚠ COMMON UNREAL ENGINE MOD WARNINGS\n\n" +
        "🖥 Black Screen on Launch\n" +
        "Upgrade `R10G10B10A2_UNORM` → `output size`\n" +
        "Unlock upgrade sliders: Settings Mode → Advanced, then restart game.\n\n" +
        "🖥 DLSS FG Flickering\n" +
        "Replace DLSSG DLL with older 3.8.x (locks FG x2) or use DLSS FIX (beta) from Discord.";

    public MainViewModel()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "RenoDXCommander/2.0");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _installer    = new ModInstallService(_http);
        _auxInstaller = new AuxInstallService(_http);
        // Subscribe to installer events — on install we'll perform a full refresh
        LoadNameMappings();
        LoadThemeAndDensity();
    }

    // --- persisted settings: name mappings, wiki exclusions, theme, density ---
    private Dictionary<string, string> _nameMappings = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Games in this set are excluded from all wiki matching.
    /// Their cards show a Discord link instead of an install button.
    /// </summary>
    private HashSet<string> _wikiExclusions   = new(StringComparer.OrdinalIgnoreCase);
    partial void OnDcModeEnabledChanged(bool v) => SaveNameMappings();

    public bool IsDcModeExcluded(string gameName)  => _dcModeExcludedGames.Contains(gameName);
    public void ToggleDcModeExclusion(string gameName)
    {
        if (_dcModeExcludedGames.Contains(gameName))
            _dcModeExcludedGames.Remove(gameName);
        else
            _dcModeExcludedGames.Add(gameName);
        SaveNameMappings();
        // Update the card immediately
        var card = _allCards.FirstOrDefault(c => c.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase));
        if (card != null) card.DcModeExcluded = _dcModeExcludedGames.Contains(gameName);
    }
    /// <summary>Games for which the user has toggled UE-Extended ON.</summary>
    private HashSet<string> _ueExtendedGames = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Games excluded from global DC Mode — always use normal file naming.</summary>
    private HashSet<string> _dcModeExcludedGames    = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _updateAllExcludedGames  = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _shaderExcludedGames     = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _is32BitGames            = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Games that default to UE-Extended and show "Extended UE Native HDR" instead of "Generic UE".
    /// These games are auto-set to UE-Extended on first build — no toggle needed.
    /// </summary>
    private static readonly HashSet<string> NativeHdrGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Avowed",
        "Lies of P",
        "Lost Soul Aside",
        "Hell is Us",
        "Mafia: The Old Country",
        "Returnal",
        "Marvel's Midnight Suns",
        "Mortal Kombat 1",
        "Alone in the Dark",
        "Still Wakes the Deep",
    };

    /// <summary>
    /// Checks if a game name matches any entry in the NativeHdrGames whitelist.
    /// Strips ™, ®, © symbols before comparison to handle store names like "Lost Soul Aside™".
    /// </summary>
    private static bool IsNativeHdrGameMatch(string gameName)
        => MatchesGameSet(gameName, NativeHdrGames);

    /// <summary>
    /// Checks if a game name matches any entry in the user's _ueExtendedGames set.
    /// Strips ™, ®, © symbols before comparison.
    /// </summary>
    private bool IsUeExtendedGameMatch(string gameName)
        => MatchesGameSet(gameName, _ueExtendedGames);

    /// <summary>
    /// Checks if <paramref name="gameName"/> matches any entry in <paramref name="gameSet"/>.
    /// Tries exact match first, then stripped (™®© removed), then fully normalised.
    /// </summary>
    private static bool MatchesGameSet(string gameName, IEnumerable<string> gameSet)
    {
        // Fast path: exact match (works for HashSet and static lists)
        if (gameSet is ICollection<string> col && col.Contains(gameName)) return true;
        if (gameSet is not ICollection<string> && gameSet.Contains(gameName)) return true;

        // Strip trademark symbols and retry
        var stripped = gameName.Replace("™", "").Replace("®", "").Replace("©", "").Trim();
        if (stripped != gameName)
        {
            if (gameSet is ICollection<string> col2 && col2.Contains(stripped)) return true;
            if (gameSet is not ICollection<string> && gameSet.Contains(stripped)) return true;
        }

        // Normalised comparison as last resort
        var norm = NormalizeForLookup(gameName);
        foreach (var entry in gameSet)
        {
            if (NormalizeForLookup(entry) == norm) return true;
        }
        return false;
    }

    public bool Is32BitGame(string gameName) => _is32BitGames.Contains(gameName);
    public void Toggle32Bit(string gameName)
    {
        if (_is32BitGames.Contains(gameName))
            _is32BitGames.Remove(gameName);
        else
            _is32BitGames.Add(gameName);
        SaveNameMappings();
        var card = _allCards.FirstOrDefault(c => c.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase));
        if (card != null)
        {
            card.Is32Bit = _is32BitGames.Contains(gameName);
            card.NotifyAll();
        }
    }

    public bool IsShaderExcluded(string gameName) => _shaderExcludedGames.Contains(gameName);
    public void ToggleShaderExclusion(string gameName)
    {
        if (_shaderExcludedGames.Contains(gameName))
            _shaderExcludedGames.Remove(gameName);
        else
            _shaderExcludedGames.Add(gameName);
        SaveNameMappings();
        var card = _allCards.FirstOrDefault(c => c.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase));
        if (card != null) card.ExcludeFromShaders = _shaderExcludedGames.Contains(gameName);
    }

    public bool AnyUpdateAvailable =>
        _allCards.Any(c => c.Status    == GameStatus.UpdateAvailable ||
                           c.DcStatus  == GameStatus.UpdateAvailable ||
                           c.RsStatus  == GameStatus.UpdateAvailable);

    // Button colours — purple when updates available, dim when idle
    public string UpdateAllBtnBackground => AnyUpdateAvailable ? "#2A1050" : "#1A1230";
    public string UpdateAllBtnForeground  => AnyUpdateAvailable ? "#D090FF" : "#886899";
    public string UpdateAllBtnBorder      => AnyUpdateAvailable ? "#7030C0" : "#3A2555";


    public bool IsUpdateAllExcluded(string gameName) => _updateAllExcludedGames.Contains(gameName);
    public void ToggleUpdateAllExclusion(string gameName)
    {
        if (_updateAllExcludedGames.Contains(gameName))
            _updateAllExcludedGames.Remove(gameName);
        else
            _updateAllExcludedGames.Add(gameName);
        SaveNameMappings();
        var card = _allCards.FirstOrDefault(c => c.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase));
        if (card != null) card.ExcludeFromUpdateAll = _updateAllExcludedGames.Contains(gameName);
    }
    private void LoadNameMappings()
    {
        try
        {
            var s = LoadSettingsFile();
            if (s.TryGetValue("NameMappings", out var json) && !string.IsNullOrEmpty(json))
                _nameMappings = JsonSerializer.Deserialize<Dictionary<string,string>>(json)
                               ?? new(StringComparer.OrdinalIgnoreCase);
            else
                _nameMappings = new(StringComparer.OrdinalIgnoreCase);

            if (s.TryGetValue("WikiExclusions", out var excJson) && !string.IsNullOrEmpty(excJson))
                _wikiExclusions = new HashSet<string>(
                    JsonSerializer.Deserialize<List<string>>(excJson) ?? new(),
                    StringComparer.OrdinalIgnoreCase);
            else
                _wikiExclusions = new(StringComparer.OrdinalIgnoreCase);

            if (s.TryGetValue("UeExtendedGames", out var ueJson) && !string.IsNullOrEmpty(ueJson))
                _ueExtendedGames = new HashSet<string>(
                    JsonSerializer.Deserialize<List<string>>(ueJson) ?? new(),
                    StringComparer.OrdinalIgnoreCase);
            else
                _ueExtendedGames = new(StringComparer.OrdinalIgnoreCase);

            if (s.TryGetValue("DcModeEnabled", out var dcMode))
                DcModeEnabled = dcMode.Equals("True", StringComparison.OrdinalIgnoreCase);

            if (s.TryGetValue("DcModeExcluded", out var dcExcJson) && !string.IsNullOrEmpty(dcExcJson))
                _dcModeExcludedGames = new HashSet<string>(
                    JsonSerializer.Deserialize<List<string>>(dcExcJson) ?? new(),
                    StringComparer.OrdinalIgnoreCase);
            else
                _dcModeExcludedGames = new(StringComparer.OrdinalIgnoreCase);

            if (s.TryGetValue("UpdateAllExcluded", out var uaJson) && !string.IsNullOrEmpty(uaJson))
                _updateAllExcludedGames = new HashSet<string>(
                    JsonSerializer.Deserialize<List<string>>(uaJson) ?? new(),
                    StringComparer.OrdinalIgnoreCase);
            else
                _updateAllExcludedGames = new(StringComparer.OrdinalIgnoreCase);

            if (s.TryGetValue("ShaderExcluded", out var seJson) && !string.IsNullOrEmpty(seJson))
                _shaderExcludedGames = new HashSet<string>(
                    JsonSerializer.Deserialize<List<string>>(seJson) ?? new(),
                    StringComparer.OrdinalIgnoreCase);
            else
                _shaderExcludedGames = new(StringComparer.OrdinalIgnoreCase);

            if (s.TryGetValue("Is32BitGames", out var b32Json) && !string.IsNullOrEmpty(b32Json))
                _is32BitGames = new HashSet<string>(
                    JsonSerializer.Deserialize<List<string>>(b32Json) ?? new(),
                    StringComparer.OrdinalIgnoreCase);
            else
                _is32BitGames = new(StringComparer.OrdinalIgnoreCase);

            if (s.TryGetValue("ShaderDeployMode", out var sdm) &&
                Enum.TryParse<ShaderDeployMode>(sdm, out var parsedSdm))
                ShaderDeployMode = parsedSdm;
            ShaderPackService.CurrentMode = ShaderDeployMode;
        }
        catch
        {
            _nameMappings           = new(StringComparer.OrdinalIgnoreCase);
            _wikiExclusions         = new(StringComparer.OrdinalIgnoreCase);
            _ueExtendedGames        = new(StringComparer.OrdinalIgnoreCase);
            _dcModeExcludedGames    = new(StringComparer.OrdinalIgnoreCase);
            _updateAllExcludedGames = new(StringComparer.OrdinalIgnoreCase);
            _shaderExcludedGames    = new(StringComparer.OrdinalIgnoreCase);
            _is32BitGames           = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void AddNameMapping(string detectedName, string wikiKey)
    {
        if (string.IsNullOrWhiteSpace(detectedName) || string.IsNullOrWhiteSpace(wikiKey)) return;
        _nameMappings[detectedName] = wikiKey;
        SaveNameMappings();
        // Rebuild cards immediately so the mapping takes effect without a manual Refresh
        DispatcherQueue?.TryEnqueue(() => { _ = InitializeAsync(forceRescan: false); });
    }

    public string? GetNameMapping(string detectedName)
    {
        if (string.IsNullOrWhiteSpace(detectedName)) return null;
        if (_nameMappings.TryGetValue(detectedName, out var v)) return v;
        // Also try normalised key
        var norm = GameDetectionService.NormalizeName(detectedName);
        foreach (var kv in _nameMappings)
            if (GameDetectionService.NormalizeName(kv.Key) == norm) return kv.Value;
        return null;
    }

    public void RemoveNameMapping(string detectedName)
    {
        if (string.IsNullOrWhiteSpace(detectedName)) return;
        _nameMappings.Remove(detectedName);
        var norm = GameDetectionService.NormalizeName(detectedName);
        var toRemove = _nameMappings.Keys
            .Where(k => GameDetectionService.NormalizeName(k) == norm).ToList();
        foreach (var k in toRemove) _nameMappings.Remove(k);
        SaveNameMappings();
        DispatcherQueue?.TryEnqueue(() => { _ = InitializeAsync(forceRescan: false); });
    }

    public bool IsWikiExcluded(string gameName) =>
        _wikiExclusions.Contains(gameName);

    /// <summary>
    /// Toggles wiki exclusion for a game and updates its card in-place — no full rescan.
    /// Excluded games show a Discord link instead of the install button.
    /// </summary>
    /// <summary>
    /// Toggles wiki exclusion for a game and updates its card synchronously in-place.
    /// This is always called from the UI thread (via dialog ContinueWith on the
    /// synchronisation context), so we update card properties directly — no
    /// DispatcherQueue.TryEnqueue needed, and the UI reflects the change immediately
    /// when the dialog closes without requiring a manual refresh.
    /// </summary>
    public void ToggleWikiExclusion(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName)) return;

        bool nowExcluded;
        if (_wikiExclusions.Contains(gameName))
        {
            _wikiExclusions.Remove(gameName);
            nowExcluded = false;
        }
        else
        {
            _wikiExclusions.Add(gameName);
            nowExcluded = true;
        }
        SaveNameMappings();

        var card = _allCards.FirstOrDefault(c =>
            c.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase));
        if (card == null)
        {
            DispatcherQueue?.TryEnqueue(() => { _ = InitializeAsync(forceRescan: false); });
            return;
        }

        if (nowExcluded)
        {
            // Exclude: strip wiki mod and show Discord button
            card.Mod           = null;
            card.IsExternalOnly = true;
            card.ExternalUrl   = "https://discord.gg/gF4GRJWZ2A";
            card.ExternalLabel = "Get on Discord";
            card.DiscordUrl    = "https://discord.gg/gF4GRJWZ2A";
            card.WikiStatus    = "💬";
            card.Notes         = "";
            card.IsGenericMod  = false;
            if (card.Status != GameStatus.Installed)
                card.Status = GameStatus.Available;
        }
        else
        {
            // Un-exclude: re-run wiki match in-place and restore the card
            var game = card.DetectedGame;
            if (game == null)
            {
                DispatcherQueue?.TryEnqueue(() => { _ = InitializeAsync(forceRescan: false); });
                return;
            }
            var (_, engine) = GameDetectionService.DetectEngineAndPath(game.InstallPath);
            var mod         = GameDetectionService.MatchGame(game, _allMods, _nameMappings);
            var fallback    = mod == null ? (engine == EngineType.Unreal ? MakeGenericUnreal()
                                           : engine == EngineType.Unity  ? MakeGenericUnity()
                                           : null) : null;
            var effectiveMod = mod ?? fallback;

            card.Mod            = effectiveMod;
            card.IsExternalOnly = effectiveMod?.SnapshotUrl == null &&
                                  (effectiveMod?.NexusUrl != null || effectiveMod?.DiscordUrl != null);
            card.ExternalUrl    = effectiveMod?.NexusUrl ?? effectiveMod?.DiscordUrl ?? "";
            card.ExternalLabel  = effectiveMod?.NexusUrl != null ? "Get on Nexus Mods" : "Get on Discord";
            card.NexusUrl       = effectiveMod?.NexusUrl;
            card.DiscordUrl     = effectiveMod?.DiscordUrl;
            card.WikiStatus     = effectiveMod?.Status ?? "—";
            card.Notes          = effectiveMod != null
                                  ? BuildNotes(game.Name, effectiveMod, fallback, _genericNotes, card.IsNativeHdrGame)
                                  : "";
            card.IsGenericMod   = card.UseUeExtended || (fallback != null && mod == null);
            if (card.Status != GameStatus.Installed)
                card.Status = effectiveMod != null ? GameStatus.Available : GameStatus.Available;
        }

        card.NotifyAll();
    }

    public const string UeExtendedUrl    = "https://marat569.github.io/renodx/renodx-ue-extended.addon64";
    public const string UeExtendedFile   = "renodx-ue-extended.addon64";
    public const string GenericUnrealFile = "renodx-unrealengine.addon64";

    /// <summary>
    /// Toggles the UE-Extended mode for a Generic UE card.
    /// When ON: Mod.SnapshotUrl → marat569 URL; if the standard generic file is on disk it is deleted.
    /// When OFF: Mod.SnapshotUrl → standard WikiService.GenericUnrealUrl; the extended file is deleted.
    /// Card updates synchronously — no refresh needed.
    /// </summary>
    public void ToggleUeExtended(GameCardViewModel card)
    {
        if (card == null || !card.IsGenericMod) return;

        bool nowExtended = !card.UseUeExtended;

        if (nowExtended)
            _ueExtendedGames.Add(card.GameName);
        else
            _ueExtendedGames.Remove(card.GameName);
        SaveNameMappings();

        // Swap the SnapshotUrl on the card's Mod in-place
        if (card.Mod != null)
            card.Mod.SnapshotUrl = nowExtended ? UeExtendedUrl : WikiService.GenericUnrealUrl;

        // Delete the opposing addon file from disk (if present)
        if (!string.IsNullOrEmpty(card.InstallPath) && Directory.Exists(card.InstallPath))
        {
            try
            {
                var deleteFile = nowExtended ? GenericUnrealFile : UeExtendedFile;
                var deletePath = Path.Combine(card.InstallPath, deleteFile);
                if (File.Exists(deletePath))
                {
                    File.Delete(deletePath);
                    CrashReporter.Log($"UE-Extended toggle: deleted {deleteFile} from {card.InstallPath}");
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"UE-Extended toggle: failed to delete file — {ex.Message}");
            }
        }

        // The toggle has swapped the target addon file. The old file was deleted above,
        // so the card is no longer "installed" — reset to Available and clear the record.
        // Leaving a stale InstalledRecord with the old RemoteFileSize would cause
        // CheckForUpdateAsync to compare the new URL's size against the old addon's size
        // and fire a false "update available" on the next refresh.
        if (card.InstalledRecord != null)
        {
            _installer.RemoveRecord(card.InstalledRecord);
            card.InstalledRecord        = null;
            card.InstalledAddonFileName = null;
            card.Status                 = GameStatus.Available;
        }

        card.UseUeExtended = nowExtended;
        card.NotifyAll();
    }

    private record CardOverride(
        string? Notes,
        string? DiscordUrl,
        bool ForceDiscord,
        string? NameUrl       = null,   // 💬 discussion button URL
        string? NotesUrl      = null,   // clickable link inside the notes dialog
        string? NotesUrlLabel = null);  // display label for that link

    /// <summary>
    /// Applies hardcoded per-game card overrides after BuildCards completes.
    /// Use this for games that need custom notes, forced Discord routing, or
    /// other card-level adjustments that can't be expressed in WikiService alone.
    /// </summary>
    private static void ApplyCardOverrides(List<GameCardViewModel> cards)
    {
        var overrides = new Dictionary<string, CardOverride>(StringComparer.OrdinalIgnoreCase)
        {
            // Cyberpunk 2077 — WIP mod, always direct to Discord for the latest build
            ["Cyberpunk 2077"] = new CardOverride(
                Notes: "⚠️ The RenoDX mod for Cyberpunk 2077 is a WIP. " +
                       "Always get the latest build directly from the RenoDX Discord — " +
                       "it is updated more frequently than any wiki download.\n\n" +
                       "See Creepy's Cyberpunk RenoDX Guide for setup instructions:",
                DiscordUrl: "https://discord.gg/gF4GRJWZ2A",
                ForceDiscord: true,
                NameUrl:       "https://www.hdrmods.com/Cyberpunk",
                NotesUrl:      "https://www.hdrmods.com/Cyberpunk",
                NotesUrlLabel: "Creepy's Cyberpunk RenoDX Guide"),
        };

        foreach (var card in cards)
        {
            if (!overrides.TryGetValue(card.GameName, out var ov)) continue;

            if (ov.ForceDiscord)
            {
                // Strip any snapshot URL so no install button appears
                if (card.Mod != null)
                    card.Mod.SnapshotUrl = null;

                card.IsExternalOnly  = true;
                card.ExternalUrl     = ov.DiscordUrl ?? "https://discord.gg/gF4GRJWZ2A";
                card.ExternalLabel   = "Get on Discord";
                card.DiscordUrl      = ov.DiscordUrl ?? "https://discord.gg/gF4GRJWZ2A";
                card.WikiStatus      = "💬";
            }

            if (!string.IsNullOrEmpty(ov.Notes))
                card.Notes = ov.Notes;

            if (!string.IsNullOrEmpty(ov.NameUrl))
                card.NameUrl = ov.NameUrl;

            if (!string.IsNullOrEmpty(ov.NotesUrl))
            {
                card.NotesUrl      = ov.NotesUrl;
                card.NotesUrlLabel = ov.NotesUrlLabel;
            }
        }
    }

    private void SaveNameMappings()
    {
        try
        {
            var s = LoadSettingsFile();
            s["NameMappings"]    = JsonSerializer.Serialize(_nameMappings);
            s["WikiExclusions"]  = JsonSerializer.Serialize(_wikiExclusions.ToList());
            s["UeExtendedGames"] = JsonSerializer.Serialize(_ueExtendedGames.ToList());
            s["DcModeEnabled"]   = DcModeEnabled.ToString();
            s["DcModeExcluded"]         = JsonSerializer.Serialize(_dcModeExcludedGames.ToList());
            s["UpdateAllExcluded"]     = JsonSerializer.Serialize(_updateAllExcludedGames.ToList());
            s["ShaderExcluded"]       = JsonSerializer.Serialize(_shaderExcludedGames.ToList());
            s["Is32BitGames"]         = JsonSerializer.Serialize(_is32BitGames.ToList());
            s["ShaderDeployMode"]    = ShaderDeployMode.ToString();
            SaveSettingsFile(s);
        }
        catch { }
    }

    private void LoadThemeAndDensity()
    {
        // Theme/density removed — no longer used
    }

    // Normalize titles for tolerant lookup: remove punctuation, trademarks, parenthetical text, diacritics
    private static string NormalizeForLookup(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // Remove common trademark symbols
        s = s.Replace("™", "").Replace("®", "").Replace("©", "");
        // Remove parenthetical content
        s = Regex.Replace(s, "\\([^)]*\\)", "");
        s = Regex.Replace(s, "\\[[^]]*\\]", "");
        // Normalize unicode and remove diacritics
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);
        // Remove punctuation, keep letters/numbers and spaces
        var cleaned = Regex.Replace(noDiacritics, "[^0-9A-Za-z ]+", " ");
        // Collapse whitespace and trim
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        // Remove common edition suffixes
        cleaned = Regex.Replace(cleaned, "\\b(enhanced edition|remastered|edition|ultimate|definitive)\\b", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        return cleaned.ToLowerInvariant();
    }

    private static string? GetGenericNote(string gameName, Dictionary<string, string> genericNotes)
    {
        if (string.IsNullOrEmpty(gameName) || genericNotes == null || genericNotes.Count == 0) return null;
        // Check user name mappings from JSON settings file
        try
        {
            var s = LoadSettingsFile();
            if (s.TryGetValue("NameMappings", out var json) && !string.IsNullOrEmpty(json))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string,string>>(json);
                if (map != null)
                {
                    if (map.TryGetValue(gameName, out var mapped) && !string.IsNullOrEmpty(mapped))
                    {
                        if (genericNotes.TryGetValue(mapped, out var mv) && !string.IsNullOrEmpty(mv)) return mv;
                    }
                    var n = NormalizeForLookup(gameName);
                    foreach (var kv in map)
                    {
                        if (NormalizeForLookup(kv.Key).Equals(n, StringComparison.OrdinalIgnoreCase))
                        {
                            if (genericNotes.TryGetValue(kv.Value, out var mv2) && !string.IsNullOrEmpty(mv2)) return mv2;
                        }
                    }
                }
            }
        }
        catch { }
        // direct
        if (genericNotes.TryGetValue(gameName, out var v) && !string.IsNullOrEmpty(v)) return v;
        // detection-normalized
        try { var k = GameDetectionService.NormalizeName(gameName); if (!string.IsNullOrEmpty(k) && genericNotes.TryGetValue(k, out var v2) && !string.IsNullOrEmpty(v2)) return v2; } catch { }
        // normalized-equality scan
        var tgt = NormalizeForLookup(gameName);
        foreach (var kv in genericNotes)
        {
            if (NormalizeForLookup(kv.Key).Equals(tgt, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        }
        return null;
    }

    // InstallCompleted event handler removed — card state is updated in-place
    // by InstallModAsync, so no full rescan is needed after install.

    // ── Commands ──────────────────────────────────────────────────────────────────

    [RelayCommand] public void SetFilter(string filter) { FilterMode = filter; ApplyFilter(); }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        // "Refresh" now performs a full rescan (previously called Full Rescan)
        await InitializeAsync(forceRescan: true);
    }

    [RelayCommand]
    public void ToggleShowHidden()
    {
        ShowHidden = !ShowHidden;
        ApplyFilter();
    }

    [RelayCommand]
    public void ToggleHideGame(GameCardViewModel? card)
    {
        if (card == null) return;
        var key = card.GameName;
        CrashReporter.Log($"ToggleHide: {key} (currently hidden={card.IsHidden})");
        if (_hiddenGames.Contains(key))
            _hiddenGames.Remove(key);
        else
            _hiddenGames.Add(key);

        card.IsHidden = _hiddenGames.Contains(key);
        SaveLibrary();
        ApplyFilter();
        UpdateCounts();
    }

    [RelayCommand]
    public void RemoveManualGame(GameCardViewModel? card)
    {
        if (card == null) return;
        if (!card.IsManuallyAdded)
            return;

        // Remove manual entries and the corresponding card
        _manualGames.RemoveAll(g => g.Name.Equals(card.GameName, StringComparison.OrdinalIgnoreCase));
        _allCards.RemoveAll(c => c.IsManuallyAdded && c.GameName.Equals(card.GameName, StringComparison.OrdinalIgnoreCase));
        SaveLibrary();
        ApplyFilter();
        UpdateCounts();
    }

    [RelayCommand]
    public void AddManualGame(DetectedGame game)
    {
        if (_manualGames.Any(g => g.Name.Equals(game.Name, StringComparison.OrdinalIgnoreCase))) return;
        _manualGames.Add(game);

        // Build card for this game immediately
        var (installPath, engine) = GameDetectionService.DetectEngineAndPath(game.InstallPath);

        // Apply per-game install path overrides (e.g. Cyberpunk 2077 → bin\x64)
        if (_installPathOverrides.TryGetValue(game.Name, out var manualSubPath))
        {
            var overridePath = Path.Combine(game.InstallPath, manualSubPath);
            if (Directory.Exists(overridePath))
                installPath = overridePath;
        }

        var mod = GameDetectionService.MatchGame(game, _allMods, _nameMappings);
        var genericUnreal = MakeGenericUnreal();
        var genericUnity  = MakeGenericUnity();
        var fallback = mod == null ? (engine == EngineType.Unreal      ? genericUnreal
                                   : engine == EngineType.Unity       ? genericUnity : null) : null;
        var effectiveMod = mod ?? fallback; // null for unknown-engine / legacy games not on wiki

        var records = _installer.LoadAll();
        var record  = records.FirstOrDefault(r => r.GameName.Equals(game.Name, StringComparison.OrdinalIgnoreCase));

        // Scan disk for any renodx-* addon file already installed
        var scanPath = installPath.Length > 0 ? installPath : game.InstallPath;
        var addonOnDisk = ScanForInstalledAddon(scanPath, effectiveMod);
        if (addonOnDisk != null && record == null)
        {
            record = new InstalledModRecord
            {
                GameName      = game.Name,
                InstallPath   = scanPath,
                AddonFileName = addonOnDisk,
                InstalledAt   = File.GetLastWriteTimeUtc(Path.Combine(scanPath, addonOnDisk)),
                SnapshotUrl   = ResolveAddonUrl(addonOnDisk),
            };
            _installer.SaveRecordPublic(record);
        }

        // Patch effectiveMod SnapshotUrl if installed addon has an override URL
        if (addonOnDisk != null && effectiveMod?.SnapshotUrl != null
            && _addonFileUrlOverrides.TryGetValue(addonOnDisk, out var addonOverrideUrlM))
        {
            effectiveMod = new GameMod
            {
                Name        = effectiveMod.Name,
                Maintainer  = effectiveMod.Maintainer,
                SnapshotUrl = addonOverrideUrlM,
                Status      = effectiveMod.Status,
                Notes       = effectiveMod.Notes,
                NexusUrl    = effectiveMod.NexusUrl,
                DiscordUrl  = effectiveMod.DiscordUrl,
                NameUrl     = effectiveMod.NameUrl,
                IsGenericUnreal = effectiveMod.IsGenericUnreal,
                IsGenericUnity  = effectiveMod.IsGenericUnity,
            };
        }

        // Named addon found on disk but no wiki entry → show Discord link
        if (addonOnDisk != null && effectiveMod == null)
        {
            effectiveMod = new GameMod
            {
                Name       = game.Name,
                Status     = "💬",
                DiscordUrl = "https://discord.gg/gF4GRJWZ2A",
            };
        }

        // ── Apply NativeHdr / UE-Extended whitelist (same logic as BuildCards) ────
        bool isNativeHdr = IsNativeHdrGameMatch(game.Name);
        bool useUeExt = (addonOnDisk == UeExtendedFile)
                        || (IsUeExtendedGameMatch(game.Name) && (effectiveMod?.IsGenericUnreal == true || engine == EngineType.Unreal))
                        || (isNativeHdr && (effectiveMod?.IsGenericUnreal == true || engine == EngineType.Unreal));
        if (useUeExt && (effectiveMod?.IsGenericUnreal == true || engine == EngineType.Unreal))
        {
            effectiveMod = new GameMod
            {
                Name            = effectiveMod?.Name ?? "Generic Unreal Engine",
                Maintainer      = effectiveMod?.Maintainer ?? "ShortFuse",
                SnapshotUrl     = UeExtendedUrl,
                Status          = effectiveMod?.Status ?? "✅",
                Notes           = effectiveMod?.Notes,
                IsGenericUnreal = true,
            };
            if (addonOnDisk == UeExtendedFile || isNativeHdr)
                _ueExtendedGames.Add(game.Name);
        }
        else if (useUeExt && effectiveMod == null)
        {
            effectiveMod = new GameMod
            {
                Name            = "Generic Unreal Engine",
                Maintainer      = "ShortFuse",
                SnapshotUrl     = UeExtendedUrl,
                Status          = "✅",
                IsGenericUnreal = true,
            };
            fallback = effectiveMod;
            if (isNativeHdr)
                _ueExtendedGames.Add(game.Name);
        }

        // UE-Extended whitelist supersedes Nexus/Discord external links
        if (useUeExt && effectiveMod != null)
        {
            effectiveMod.NexusUrl   = null;
            effectiveMod.DiscordUrl = null;
        }

        var auxRecordsManual = _auxInstaller.LoadAll();
        var dcRecManual = auxRecordsManual.FirstOrDefault(r =>
            r.GameName.Equals(game.Name, StringComparison.OrdinalIgnoreCase) &&
            r.AddonType == AuxInstallService.TypeDc);
        var rsRecManual = auxRecordsManual.FirstOrDefault(r =>
            r.GameName.Equals(game.Name, StringComparison.OrdinalIgnoreCase) &&
            r.AddonType == AuxInstallService.TypeReShade);

        var card = new GameCardViewModel
        {
            GameName       = game.Name,
            Mod            = effectiveMod,
            DetectedGame   = game,
            InstallPath    = scanPath,
            Source         = "Manual",
            InstalledRecord = record,
            Status         = record != null ? GameStatus.Installed : GameStatus.Available,
            WikiStatus     = (_wikiExclusions.Contains(game.Name)
                               || (effectiveMod?.SnapshotUrl == null && effectiveMod?.DiscordUrl != null && effectiveMod?.NexusUrl == null))
                              ? "💬"
                              : effectiveMod?.Status ?? "—",
            Maintainer     = effectiveMod?.Maintainer ?? "",
            IsGenericMod   = useUeExt || (fallback != null && mod == null),
            EngineHint     = (useUeExt && engine == EngineType.Unknown) ? "Unreal Engine"
                           : engine == EngineType.Unreal       ? "Unreal Engine"
                           : engine == EngineType.UnrealLegacy ? "Unreal (Legacy)"
                           : engine == EngineType.Unity        ? "Unity" : "",
            Notes          = effectiveMod != null ? BuildNotes(game.Name, effectiveMod, fallback, _genericNotes, isNativeHdr) : "",
            InstalledAddonFileName = record?.AddonFileName,
            IsExternalOnly  = _wikiExclusions.Contains(game.Name)
                              ? true
                              : effectiveMod?.SnapshotUrl == null &&
                                (effectiveMod?.NexusUrl != null || effectiveMod?.DiscordUrl != null),
            ExternalUrl     = _wikiExclusions.Contains(game.Name)
                              ? "https://discord.gg/gF4GRJWZ2A"
                              : effectiveMod?.NexusUrl ?? effectiveMod?.DiscordUrl ?? "",
            ExternalLabel   = _wikiExclusions.Contains(game.Name)
                              ? "Get on Discord"
                              : effectiveMod?.NexusUrl != null ? "Get on Nexus Mods" : "Get on Discord",
            NexusUrl        = effectiveMod?.NexusUrl,
            DiscordUrl      = _wikiExclusions.Contains(game.Name)
                              ? "https://discord.gg/gF4GRJWZ2A"
                              : effectiveMod?.DiscordUrl,
            NameUrl         = effectiveMod?.NameUrl,
            IsManuallyAdded = true,
            UseUeExtended          = useUeExt,
            IsNativeHdrGame        = isNativeHdr,
            DcModeExcluded         = _dcModeExcludedGames.Contains(game.Name),
            ExcludeFromUpdateAll   = _updateAllExcludedGames.Contains(game.Name),
            ExcludeFromShaders     = _shaderExcludedGames.Contains(game.Name),
            Is32Bit                = _is32BitGames.Contains(game.Name),
            DcRecord        = dcRecManual,
            DcStatus        = dcRecManual != null ? GameStatus.Installed : GameStatus.NotInstalled,
            DcInstalledFile = dcRecManual?.InstalledAs,
            RsRecord        = rsRecManual,
            RsStatus        = rsRecManual != null ? GameStatus.Installed : GameStatus.NotInstalled,
            RsInstalledFile = rsRecManual?.InstalledAs,
        };

        _allCards.Add(card);
        SaveLibrary();
        ApplyFilter();
        UpdateCounts();
    }

    [RelayCommand]
    public async Task InstallModAsync(GameCardViewModel? card)
    {
        // Install invoked
        if (card?.Mod?.SnapshotUrl == null) return;

        // 32-bit toggle: swap URL before install, restore after
        string? originalSnapshotUrl = card.Mod.SnapshotUrl;
        bool swappedTo32 = card.Is32Bit && card.Mod.SnapshotUrl32 != null;
        if (swappedTo32)
            card.Mod.SnapshotUrl = card.Mod.SnapshotUrl32;
        if (string.IsNullOrEmpty(card.InstallPath))
        {
            card.ActionMessage = "No install path — use 📁 to pick the game folder.";
            return;
        }
        card.IsInstalling = true;
        card.ActionMessage = "Starting download...";
        CrashReporter.Log($"Install started: {card.GameName} → {card.InstallPath}");
        try
        {
            var progress = new Progress<(string msg, double pct)>(p =>
            {
                card.ActionMessage   = p.msg;
                card.InstallProgress = p.pct;
            });
            var record = await _installer.InstallAsync(card.Mod, card.InstallPath, progress);

            // Update only this card's observable properties in-place.
            // The card is already in DisplayedGames — WinUI bindings update the
            // card visually the moment each property changes. No collection
            // manipulation (Clear/Add) is needed, so the rest of the UI is untouched.
            DispatcherQueue?.TryEnqueue(() =>
            {
                card.InstalledRecord        = record;
                card.InstalledAddonFileName = record.AddonFileName;
                card.Status                 = GameStatus.Installed;
                card.ActionMessage          = "✅ Installed! Press Home in-game to open ReShade.";
                CrashReporter.Log($"Install complete: {card.GameName} — {record.AddonFileName}");
                card.NotifyAll();
                SaveLibrary();
                // Recalculate counts only — do NOT call ApplyFilter() which
                // would Clear() + re-add every card and flash the whole UI.
                InstalledCount = _allCards.Count(c => c.Status == GameStatus.Installed || c.Status == GameStatus.UpdateAvailable);
                TotalGames     = DisplayedGames.Count;
                OnPropertyChanged(nameof(InstalledCount));
                OnPropertyChanged(nameof(TotalGames));
            });
        }
        catch (Exception ex)
        {
            card.ActionMessage = $"❌ Failed: {ex.Message}";
            CrashReporter.WriteCrashReport("InstallModAsync", ex, note: $"Game: {card.GameName}, Path: {card.InstallPath}");
        }
        finally
        {
            card.IsInstalling = false;
            // Restore original URL if we swapped to 32-bit for the install
            if (swappedTo32 && card.Mod != null && originalSnapshotUrl != null)
                card.Mod.SnapshotUrl = originalSnapshotUrl;
        }
    }

    [RelayCommand]
    public async Task InstallMod32Async(GameCardViewModel? card)
    {
        if (card?.Mod?.SnapshotUrl32 == null) return;
        var orig = card.Mod.SnapshotUrl;
        card.Mod.SnapshotUrl = card.Mod.SnapshotUrl32;
        await InstallModAsync(card);
        card.Mod.SnapshotUrl = orig;
    }

    [RelayCommand]
    public void UninstallMod(GameCardViewModel? card)
    {
        if (card?.InstalledRecord == null) return;
        CrashReporter.Log($"Uninstall: {card.GameName}");
        _installer.Uninstall(card.InstalledRecord);
        card.InstalledRecord        = null;
        card.InstalledAddonFileName = null;
        card.Status                 = GameStatus.Available;
        card.ActionMessage          = "Mod removed.";
        UpdateCounts();
    }

    // ── Display Commander commands ────────────────────────────────────────────────

    [RelayCommand]
    public async Task InstallDcAsync(GameCardViewModel? card)
    {
        if (card == null) return;
        if (string.IsNullOrEmpty(card.InstallPath) || !Directory.Exists(card.InstallPath))
        {
            card.DcActionMessage = "No install path — use 📁 to pick the game folder.";
            return;
        }

        // Check for foreign dxgi.dll before overwriting
        var effectiveDcMode = DcModeEnabled && !card.DcModeExcluded;
        if (effectiveDcMode)
        {
            var dxgiPath = Path.Combine(card.InstallPath, "dxgi.dll");
            if (File.Exists(dxgiPath))
            {
                var fileType = AuxInstallService.IdentifyDxgiFile(dxgiPath);
                if (fileType == AuxInstallService.DxgiFileType.Unknown)
                {
                    // Ask the UI for confirmation via async callback
                    if (ConfirmForeignDxgiOverwrite != null)
                    {
                        var confirmed = await ConfirmForeignDxgiOverwrite(card, dxgiPath);
                        if (!confirmed)
                        {
                            card.DcActionMessage = "⚠ Skipped — unknown dxgi.dll found. Use Overrides to proceed.";
                            return;
                        }
                    }
                    else
                    {
                        card.DcActionMessage = "⚠ Skipped — unknown dxgi.dll found.";
                        return;
                    }
                }
            }
        }

        card.DcIsInstalling  = true;
        card.DcActionMessage = "Starting DC download...";
        try
        {
            var progress = new Progress<(string msg, double pct)>(p =>
            {
                card.DcActionMessage = p.msg;
                card.DcProgress      = p.pct;
            });
            var record = await _auxInstaller.InstallDcAsync(card.GameName, card.InstallPath, effectiveDcMode,
                existingDcRecord: card.DcRecord,
                existingRsRecord: card.RsRecord,
                shaderExcluded:   card.ExcludeFromShaders,
                use32Bit:         card.Is32Bit,
                progress:         progress);
            DispatcherQueue?.TryEnqueue(() =>
            {
                card.DcRecord        = record;
                card.DcInstalledFile = record.InstalledAs;
                card.DcStatus        = GameStatus.Installed;
                card.DcActionMessage = "✅ Display Commander installed!";
                card.NotifyAll();
            });
        }
        catch (Exception ex)
        {
            card.DcActionMessage = $"❌ DC Failed: {ex.Message}";
            CrashReporter.WriteCrashReport("InstallDcAsync", ex, note: $"Game: {card.GameName}");
        }
        finally { card.DcIsInstalling = false; }
    }

    [RelayCommand]
    public void UninstallDc(GameCardViewModel? card)
    {
        if (card?.DcRecord == null) return;
        try
        {
            _auxInstaller.Uninstall(card.DcRecord);
            card.DcRecord        = null;
            card.DcInstalledFile = null;
            card.DcStatus        = GameStatus.NotInstalled;
            card.DcActionMessage = "Display Commander removed.";
            card.NotifyAll();
        }
        catch (Exception ex)
        {
            card.DcActionMessage = $"❌ Uninstall failed: {ex.Message}";
            CrashReporter.WriteCrashReport("UninstallDc", ex, note: $"Game: {card.GameName}");
        }
    }

    // ── ReShade commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task InstallReShadeAsync(GameCardViewModel? card)
    {
        if (card == null) return;
        if (string.IsNullOrEmpty(card.InstallPath) || !Directory.Exists(card.InstallPath))
        {
            card.RsActionMessage = "No install path — use 📁 to pick the game folder.";
            return;
        }

        // Check for foreign dxgi.dll before overwriting (when DC mode is OFF, ReShade installs as dxgi.dll)
        var effectiveDcModeRs = DcModeEnabled && !card.DcModeExcluded;
        if (!effectiveDcModeRs)
        {
            var dxgiPath = Path.Combine(card.InstallPath, "dxgi.dll");
            if (File.Exists(dxgiPath))
            {
                var fileType = AuxInstallService.IdentifyDxgiFile(dxgiPath);
                if (fileType == AuxInstallService.DxgiFileType.Unknown)
                {
                    if (ConfirmForeignDxgiOverwrite != null)
                    {
                        var confirmed = await ConfirmForeignDxgiOverwrite(card, dxgiPath);
                        if (!confirmed)
                        {
                            card.RsActionMessage = "⚠ Skipped — unknown dxgi.dll found. Use Overrides to proceed.";
                            return;
                        }
                    }
                    else
                    {
                        card.RsActionMessage = "⚠ Skipped — unknown dxgi.dll found.";
                        return;
                    }
                }
            }
        }

        card.RsIsInstalling  = true;
        card.RsActionMessage = "Starting ReShade download...";
        try
        {
            var progress = new Progress<(string msg, double pct)>(p =>
            {
                card.RsActionMessage = p.msg;
                card.RsProgress      = p.pct;
            });
            var record = await _auxInstaller.InstallReShadeAsync(card.GameName, card.InstallPath, effectiveDcModeRs,
                dcIsInstalled:  card.DcStatus == GameStatus.Installed,
                shaderExcluded: card.ExcludeFromShaders,
                use32Bit:       card.Is32Bit,
                progress:       progress);
            DispatcherQueue?.TryEnqueue(() =>
            {
                card.RsRecord        = record;
                card.RsInstalledFile = record.InstalledAs;
                card.RsStatus        = GameStatus.Installed;
                card.RsActionMessage = "✅ ReShade installed!";
                card.NotifyAll();
            });
        }
        catch (Exception ex)
        {
            card.RsActionMessage = $"❌ ReShade Failed: {ex.Message}";
            CrashReporter.WriteCrashReport("InstallReShadeAsync", ex, note: $"Game: {card.GameName}");
        }
        finally { card.RsIsInstalling = false; }
    }

    [RelayCommand]
    public void UninstallReShade(GameCardViewModel? card)
    {
        if (card?.RsRecord == null) return;

        try
        {
            // Remove the RDXC-managed reshade-shaders folder BEFORE calling Uninstall.
            if (card.DcStatus != GameStatus.Installed && card.DcStatus != GameStatus.UpdateAvailable)
            {
                if (!string.IsNullOrEmpty(card.InstallPath))
                    ShaderPackService.RemoveFromGameFolder(card.InstallPath);
            }

            _auxInstaller.Uninstall(card.RsRecord);
            card.RsRecord        = null;
            card.RsInstalledFile = null;
            card.RsStatus        = GameStatus.NotInstalled;
            card.RsActionMessage = "ReShade removed.";
            card.NotifyAll();
        }
        catch (Exception ex)
        {
            card.RsActionMessage = $"❌ Uninstall failed: {ex.Message}";
            CrashReporter.WriteCrashReport("UninstallReShade", ex, note: $"Game: {card.GameName}");
        }
    }

    // ── Update All commands ──────────────────────────────────────────────────────

    /// <summary>
    /// Eligibility: card must not be hidden, not excluded from Update All.
    /// </summary>
    private IEnumerable<GameCardViewModel> UpdateAllEligible() =>
        _allCards.Where(c => !c.IsHidden && !c.ExcludeFromUpdateAll
                          && !string.IsNullOrEmpty(c.InstallPath)
                          && Directory.Exists(c.InstallPath));

    public async Task UpdateAllRenoDxAsync()
    {
        var targets = UpdateAllEligible()
            .Where(c => c.Status == GameStatus.Installed || c.Status == GameStatus.UpdateAvailable)
            .Where(c => c.Mod?.SnapshotUrl != null)
            .ToList();

        foreach (var card in targets)
        {
            card.IsInstalling  = true;
            card.ActionMessage = "Updating...";
            try
            {
                var progress = new Progress<(string msg, double pct)>(p =>
                {
                    card.ActionMessage   = p.msg;
                    card.InstallProgress = p.pct;
                });
                var record = await _installer.InstallAsync(card.Mod!, card.InstallPath, progress);
                DispatcherQueue?.TryEnqueue(() =>
                {
                    card.InstalledRecord        = record;
                    card.InstalledAddonFileName = record.AddonFileName;
                    card.Status                 = GameStatus.Installed;
                    card.ActionMessage          = "✅ Updated!";
                    card.NotifyAll();
                });
            }
            catch (Exception ex)
            {
                card.ActionMessage = $"❌ Failed: {ex.Message}";
            }
            finally { card.IsInstalling = false; }
        }

        SaveLibrary();
        DispatcherQueue?.TryEnqueue(() =>
        {
            UpdateCounts();
            OnPropertyChanged(nameof(AnyUpdateAvailable));
        });
    }

    public async Task UpdateAllReShadeAsync()
    {
        var targets = UpdateAllEligible()
            .Where(c => c.RsStatus == GameStatus.Installed || c.RsStatus == GameStatus.UpdateAvailable)
            .ToList();

        foreach (var card in targets)
        {
            // Skip games with foreign dxgi.dll during Update All (when DC mode is OFF, RS = dxgi.dll)
            var effectiveDcMode = DcModeEnabled && !card.DcModeExcluded;
            if (!effectiveDcMode)
            {
                var dxgiPath = Path.Combine(card.InstallPath, "dxgi.dll");
                if (File.Exists(dxgiPath))
                {
                    var fileType = AuxInstallService.IdentifyDxgiFile(dxgiPath);
                    if (fileType == AuxInstallService.DxgiFileType.Unknown)
                    {
                        CrashReporter.Log($"UpdateAllReShade: skipping {card.GameName} — foreign dxgi.dll detected");
                        DispatcherQueue?.TryEnqueue(() =>
                        {
                            card.RsActionMessage = "⚠ Skipped — unknown dxgi.dll";
                        });
                        continue;
                    }
                }
            }

            card.RsIsInstalling  = true;
            card.RsActionMessage = "Updating...";
            try
            {
                var progress = new Progress<(string msg, double pct)>(p =>
                {
                    card.RsActionMessage = p.msg;
                    card.RsProgress      = p.pct;
                });
                // Respect DC Mode toggle and per-game exclusion
                var dcInstalled     = card.DcStatus == GameStatus.Installed
                                  || card.DcStatus == GameStatus.UpdateAvailable;
                var record = await _auxInstaller.InstallReShadeAsync(
                    card.GameName, card.InstallPath, effectiveDcMode,
                    dcIsInstalled:  dcInstalled,
                    shaderExcluded: card.ExcludeFromShaders,
                    use32Bit:       card.Is32Bit,
                    progress:       progress);
                DispatcherQueue?.TryEnqueue(() =>
                {
                    card.RsRecord        = record;
                    card.RsInstalledFile = record.InstalledAs;
                    card.RsStatus        = GameStatus.Installed;
                    card.RsActionMessage = "✅ Updated!";
                    card.NotifyAll();
                });
            }
            catch (Exception ex)
            {
                card.RsActionMessage = $"❌ Failed: {ex.Message}";
                CrashReporter.WriteCrashReport("UpdateAllReShade", ex, note: $"Game: {card.GameName}");
            }
            finally { card.RsIsInstalling = false; }
        }

        DispatcherQueue?.TryEnqueue(() => OnPropertyChanged(nameof(AnyUpdateAvailable)));
    }

    public async Task UpdateAllDcAsync()
    {
        var targets = UpdateAllEligible()
            .Where(c => c.DcStatus == GameStatus.Installed || c.DcStatus == GameStatus.UpdateAvailable)
            .ToList();

        foreach (var card in targets)
        {
            // Skip games with foreign dxgi.dll during Update All — keep purple button
            var effectiveDcMode = DcModeEnabled && !card.DcModeExcluded;
            if (effectiveDcMode)
            {
                var dxgiPath = Path.Combine(card.InstallPath, "dxgi.dll");
                if (File.Exists(dxgiPath))
                {
                    var fileType = AuxInstallService.IdentifyDxgiFile(dxgiPath);
                    if (fileType == AuxInstallService.DxgiFileType.Unknown)
                    {
                        CrashReporter.Log($"UpdateAllDc: skipping {card.GameName} — foreign dxgi.dll detected");
                        DispatcherQueue?.TryEnqueue(() =>
                        {
                            card.DcActionMessage = "⚠ Skipped — unknown dxgi.dll";
                        });
                        continue;
                    }
                }
            }

            card.DcIsInstalling  = true;
            card.DcActionMessage = "Updating...";
            try
            {
                var progress = new Progress<(string msg, double pct)>(p =>
                {
                    card.DcActionMessage = p.msg;
                    card.DcProgress      = p.pct;
                });
                // Respect DC Mode toggle and per-game exclusion
                var record = await _auxInstaller.InstallDcAsync(
                    card.GameName, card.InstallPath, effectiveDcMode,
                    existingDcRecord: card.DcRecord,
                    existingRsRecord: card.RsRecord,
                    shaderExcluded:   card.ExcludeFromShaders,
                    use32Bit:         card.Is32Bit,
                    progress:         progress);
                DispatcherQueue?.TryEnqueue(() =>
                {
                    card.DcRecord        = record;
                    card.DcInstalledFile = record.InstalledAs;
                    card.DcStatus        = GameStatus.Installed;
                    card.DcActionMessage = "✅ Updated!";
                    card.NotifyAll();
                });
            }
            catch (Exception ex)
            {
                card.DcActionMessage = $"❌ Failed: {ex.Message}";
                CrashReporter.WriteCrashReport("UpdateAllDc", ex, note: $"Game: {card.GameName}");
            }
            finally { card.DcIsInstalling = false; }
        }

        DispatcherQueue?.TryEnqueue(() => OnPropertyChanged(nameof(AnyUpdateAvailable)));
    }

    // ── Init ──────────────────────────────────────────────────────────────────────

    public async Task InitializeAsync(bool forceRescan = false)
    {
        IsLoading = true;
        DisplayedGames.Clear();
        _allCards.Clear();

        CrashReporter.Log($"InitializeAsync started (forceRescan={forceRescan})");
        try
        {
            var savedLib = GameLibraryService.Load();
            List<DetectedGame> detectedGames;
            Dictionary<string, bool> addonCache;

            _hiddenGames = savedLib?.HiddenGames ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _manualGames = savedLib != null ? GameLibraryService.ToManualGames(savedLib) : new();

            if (savedLib != null && !forceRescan)
            {
                StatusText    = $"Library loaded ({savedLib.Games.Count} games, scanned {FormatAge(savedLib.LastScanned)})";
                SubStatusText = "Checking for new games and fetching latest mod info...";
                addonCache    = savedLib.AddonScanCache;

                // Always re-detect games so newly installed titles (especially Xbox) appear
                // without requiring the user to delete cache files or manually refresh.
                var wikiTask   = WikiService.FetchAllAsync(_http);
                var detectTask = Task.Run(DetectAllGamesDeduped);
                await Task.WhenAll(wikiTask, detectTask);
                _allMods      = wikiTask.Result.Mods;
                _genericNotes = wikiTask.Result.GenericNotes;

                var freshGames = detectTask.Result;
                var cachedGames = GameLibraryService.ToDetectedGames(savedLib);

                // Merge: start with fresh scan, then add any cached games that weren't re-detected
                // (e.g. games on a disconnected drive). Fresh scan wins for duplicates.
                var freshNorms = freshGames.Select(g => GameDetectionService.NormalizeName(g.Name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                detectedGames = freshGames
                    .Concat(cachedGames.Where(g => !freshNorms.Contains(GameDetectionService.NormalizeName(g.Name))))
                    .ToList();

                CrashReporter.Log($"Merged library: {freshGames.Count} detected + {cachedGames.Count} cached → {detectedGames.Count} total");
            }
            else
            {
                StatusText    = "Scanning game library...";
                SubStatusText = "Running store scans + wiki fetch simultaneously...";
                var wikiTask   = WikiService.FetchAllAsync(_http);
                var detectTask = Task.Run(DetectAllGamesDeduped);
                await Task.WhenAll(wikiTask, detectTask);
                _allMods      = wikiTask.Result.Mods;
                _genericNotes = wikiTask.Result.GenericNotes;
                detectedGames = detectTask.Result;
                addonCache    = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                CrashReporter.Log($"Wiki fetch complete: {_allMods.Count} mods. Store scan complete: {detectedGames.Count} games.");
            }

            // Combine auto-detected + manual games.
            // Manual games override auto-detected ones with the same name.
            var manualNames = _manualGames.Select(g => GameDetectionService.NormalizeName(g.Name))
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allGames = detectedGames
                .Where(g => !manualNames.Contains(GameDetectionService.NormalizeName(g.Name)))
                .Concat(_manualGames)
                .ToList();

            var records    = _installer.LoadAll();
            var auxRecords = _auxInstaller.LoadAll();
            SubStatusText = "Matching mods and checking install status...";
            CrashReporter.Log($"Building cards for {allGames.Count} games...");
            _allCards = await Task.Run(() => BuildCards(allGames, records, auxRecords, addonCache, _genericNotes));
            CrashReporter.Log($"BuildCards complete: {_allCards.Count} cards.");

            // Check for updates (async, parallel, non-blocking)
            CrashReporter.Log("Starting background update checks...");
            _ = Task.Run(() => CheckForUpdatesAsync(_allCards, records, auxRecords));

            _allCards = _allCards.OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase).ToList();
            SaveLibrary();
            UpdateCounts();
            ApplyFilter();

            // Sync shaders to all installed locations to reflect current deploy mode.
            // Runs on a background thread — never blocks the UI.
            SubStatusText = "Deploying shaders to installed games...";
            _ = Task.Run(() =>
            {
                try
                {
                    var locations = _allCards
                        .Where(card => !string.IsNullOrEmpty(card.InstallPath))
                        .Select(card => (
                            installPath  : card.InstallPath,
                            dcInstalled  : card.DcStatus  == GameStatus.Installed || card.DcStatus  == GameStatus.UpdateAvailable,
                            rsInstalled  : card.RsStatus  == GameStatus.Installed || card.RsStatus  == GameStatus.UpdateAvailable,
                            dcMode       : DcModeEnabled && !card.DcModeExcluded,
                            shaderExcluded: card.ExcludeFromShaders
                        ));
                    ShaderPackService.SyncShadersToAllLocations(locations);
                }
                catch (Exception ex)
                { CrashReporter.Log($"SyncShaders: {ex.Message}"); }
                finally
                {
                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        SubStatusText = "";
                    });
                }
            });

            StatusText    = $"{detectedGames.Count} games detected · {InstalledCount} mods installed";
            SubStatusText = "";
        }
        catch (Exception ex)
        {
            StatusText = "Error loading";
            SubStatusText = ex.Message;
            CrashReporter.WriteCrashReport("InitializeAsync", ex);
        }
        finally { IsLoading = false; }
    }

    // ── Update checking ───────────────────────────────────────────────────────────

    private async Task CheckForUpdatesAsync(List<GameCardViewModel> cards, List<InstalledModRecord> records, List<AuxInstalledRecord> auxRecords)
    {
        // Check all installed cards that have a SnapshotUrl and are directly installable.
        // Exclude IsExternalOnly cards (Nexus/Discord-only mods) — we have no way to
        // check for updates on external sources.
        // Don't filter on RemoteFileSize — the download-based check path
        // (used for UE-Extended and other github.io CDN mods) doesn't need it.
        var installed = cards
            .Where(c => c.Status == GameStatus.Installed
                     && !c.IsExternalOnly
                     && c.Mod?.SnapshotUrl != null
                     && c.InstalledRecord?.SnapshotUrl != null)
            .ToList();

        var tasks = installed.Select(async card =>
        {
            var record = card.InstalledRecord!;

            bool updateAvailable;
            try
            {
                updateAvailable = await _installer.CheckForUpdateAsync(record);
            }
            catch { return; }

            if (updateAvailable)
            {
                _installer.SaveRecordPublic(record);
                DispatcherQueue?.TryEnqueue(() => { card.Status = GameStatus.UpdateAvailable; });
            }
        });

        await Task.WhenAll(tasks);

        // ── Aux (DC / ReShade) update checks ──────────────────────────────────────
        var auxInstalled = cards
            .Where(c => c.DcStatus == GameStatus.Installed || c.RsStatus == GameStatus.Installed)
            .ToList();

        var auxTasks = auxInstalled.SelectMany(card => new[]
        {
            card.DcRecord != null ? CheckAuxUpdate(card, card.DcRecord, isRs: false) : Task.CompletedTask,
            card.RsRecord != null ? CheckAuxUpdate(card, card.RsRecord, isRs: true)  : Task.CompletedTask,
        });

        await Task.WhenAll(auxTasks);

        // Notify the UI so the Update All button colour updates after scan
        DispatcherQueue?.TryEnqueue(() => OnPropertyChanged(nameof(AnyUpdateAvailable)));
    }

    private async Task CheckAuxUpdate(GameCardViewModel card, AuxInstalledRecord record, bool isRs)
    {
        try
        {
            bool upd = await _auxInstaller.CheckForUpdateAsync(record);
            if (upd)
                DispatcherQueue?.TryEnqueue(() =>
                {
                    if (isRs) card.RsStatus = GameStatus.UpdateAvailable;
                    else      card.DcStatus = GameStatus.UpdateAvailable;
                });
        }
        catch { }
    }

    // Dispatcher reference for cross-thread UI updates
    private Microsoft.UI.Dispatching.DispatcherQueue? DispatcherQueue { get; set; }
    public void SetDispatcher(Microsoft.UI.Dispatching.DispatcherQueue dq) => DispatcherQueue = dq;

    // ── Detection ─────────────────────────────────────────────────────────────────

    private static List<DetectedGame> DetectAllGamesDeduped()
    {
        var tasks = new[]
        {
            Task.Run(GameDetectionService.FindSteamGames),
            Task.Run(GameDetectionService.FindGogGames),
            Task.Run(GameDetectionService.FindEpicGames),
            Task.Run(GameDetectionService.FindEaGames),
            Task.Run(GameDetectionService.FindXboxGames),
        };
        Task.WhenAll(tasks).Wait();

        var all = tasks.SelectMany(t => t.Result).ToList();

        // Step 1: deduplicate exact same name from multiple stores
        var byName = all
            .GroupBy(g => GameDetectionService.NormalizeName(g.Name))
            .Select(grp => grp.First())
            .ToList();

        // Step 2: deduplicate by install path — Steam registers DLC and tools as
        // separate entries that point to the same game folder. For each unique path,
        // keep the entry with the shortest name (base game title is always shortest).
        // This collapses "Cyberpunk 2077 / Phantom Liberty / REDmod" → "Cyberpunk 2077".
        var byPath = byName
            .GroupBy(g => g.InstallPath.TrimEnd('\\', '/').ToLowerInvariant())
            .Select(grp => grp.OrderBy(g => g.Name.Length).First())
            .ToList();

        // Permanently exclude specific non-game entries
        var permanentExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Lossless Scaling",
            "Steamworks Common Redistributables",
        };
        byPath = byPath.Where(g => !permanentExclusions.Contains(g.Name)).ToList();

        return byPath;
    }

    // ── Card building ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Addon filenames that are hosted at a URL different from the standard RenoDX CDN.
    /// Used to override both the mod's SnapshotUrl (install button) and the
    /// InstalledModRecord.SnapshotUrl (update detection) whenever the file is found on disk.
    /// </summary>
    private static readonly Dictionary<string, string> _addonFileUrlOverrides =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["renodx-ue-extended.addon64"] = "https://marat569.github.io/renodx/renodx-ue-extended.addon64",
    };

    /// <summary>
    /// Per-game install path overrides: maps game name to a sub-path relative to the
    /// detected root. Used when the game exe lives in a non-standard location that the
    /// engine-detection heuristics do not resolve automatically.
    /// </summary>
    private static readonly Dictionary<string, string> _installPathOverrides =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["Cyberpunk 2077"] = @"bin\x64",
    };

    /// <summary>
    /// Returns the authoritative download URL for a given addon filename,
    /// substituting an override when the file has a known alternative source.
    /// Falls back to the generic Unreal URL for all other .addon64 files.
    /// </summary>
    private static string ResolveAddonUrl(string addonFileName)
    {
        if (_addonFileUrlOverrides.TryGetValue(addonFileName, out var url))
            return url;
        // Default: use the standard RenoDX snapshot CDN derived from the filename
        return $"https://clshortfuse.github.io/renodx/{addonFileName}";
    }

    private GameMod MakeGenericUnreal() => new()
    {
        Name = "Generic Unreal Engine", Maintainer = "ShortFuse",
        SnapshotUrl = WikiService.GenericUnrealUrl, Status = "✅", IsGenericUnreal = true
    };
    private GameMod MakeGenericUnity() => new()
    {
        Name = "Generic Unity Engine", Maintainer = "ShortFuse",
        SnapshotUrl = WikiService.GenericUnityUrl64, SnapshotUrl32 = WikiService.GenericUnityUrl32,
        Status = "✅", IsGenericUnity = true
    };

    private List<GameCardViewModel> BuildCards(
        List<DetectedGame> detectedGames,
        List<InstalledModRecord> records,
        List<AuxInstalledRecord> auxRecords,
        Dictionary<string, bool> addonCache,
        Dictionary<string, string> genericNotes)
    {
        var cards = new List<GameCardViewModel>();
        var genericUnreal = MakeGenericUnreal();
        var genericUnity  = MakeGenericUnity();

        var gameInfos = detectedGames.AsParallel().Select(game =>
        {
            var (installPath, engine) = GameDetectionService.DetectEngineAndPath(game.InstallPath);

            // Apply per-game install path overrides (e.g. Cyberpunk 2077 → bin\x64)
            if (_installPathOverrides.TryGetValue(game.Name, out var subPath))
            {
                var overridePath = Path.Combine(game.InstallPath, subPath);
                if (Directory.Exists(overridePath))
                    installPath = overridePath;
            }

            var mod      = GameDetectionService.MatchGame(game, _allMods, _nameMappings);
            // UnrealLegacy (UE3 and below) cannot use the RenoDX addon system — no fallback mod offered.
            var fallback = mod == null ? (engine == EngineType.Unreal      ? genericUnreal
                                        : engine == EngineType.Unity       ? genericUnity : null) : null;
            return (game, installPath, engine, mod, fallback);
        }).ToList();

        foreach (var (game, installPath, engine, mod, origFallback) in gameInfos)
        {
            // Always show every detected game — even if no wiki mod exists.
            // The card will have no install button if there's no snapshot URL,
            // but a RenoDX addon already on disk will still be detected and shown.
            // Wiki exclusion overrides everything — user explicitly wants no wiki match
            var fallback     = origFallback;  // mutable local copy
            var effectiveMod = _wikiExclusions.Contains(game.Name) ? null : (mod ?? fallback);

            var record = records.FirstOrDefault(r =>
                r.GameName.Equals(game.Name, StringComparison.OrdinalIgnoreCase));

            // Always scan disk for renodx-* addon files — catches manual installs and
            // games not yet on the wiki that already have a mod installed.
            string? addonOnDisk = null;
            var cacheKey = installPath.ToLowerInvariant();
            if (addonCache.TryGetValue(cacheKey, out var cached))
            {
                if (cached) addonOnDisk = ScanForInstalledAddon(installPath, effectiveMod);
            }
            else
            {
                addonOnDisk = ScanForInstalledAddon(installPath, effectiveMod);
                addonCache[cacheKey] = addonOnDisk != null;
            }

            if (addonOnDisk != null && record == null)
            {
                // Use ResolveAddonUrl so files like renodx-ue-extended.addon64 get their
                // correct source URL rather than the generic CDN URL from effectiveMod.
                record = new InstalledModRecord
                {
                    GameName      = game.Name,
                    InstallPath   = installPath,
                    AddonFileName = addonOnDisk,
                    InstalledAt   = File.GetLastWriteTimeUtc(Path.Combine(installPath, addonOnDisk)),
                    SnapshotUrl   = ResolveAddonUrl(addonOnDisk),
                };
                _installer.SaveRecordPublic(record);
            }

            // If the installed addon on disk has a different source URL than what the
            // wiki mod specifies (e.g. renodx-ue-extended.addon64 on a generic UE card),
            // patch effectiveMod so the install/update button uses the correct URL.
            if (addonOnDisk != null && effectiveMod?.SnapshotUrl != null
                && _addonFileUrlOverrides.TryGetValue(addonOnDisk, out var addonOverrideUrl))
            {
                effectiveMod = new GameMod
                {
                    Name        = effectiveMod.Name,
                    Maintainer  = effectiveMod.Maintainer,
                    SnapshotUrl = addonOverrideUrl,
                    Status      = effectiveMod.Status,
                    Notes       = effectiveMod.Notes,
                    NexusUrl    = effectiveMod.NexusUrl,
                    DiscordUrl  = effectiveMod.DiscordUrl,
                    NameUrl     = effectiveMod.NameUrl,
                    IsGenericUnreal = effectiveMod.IsGenericUnreal,
                    IsGenericUnity  = effectiveMod.IsGenericUnity,
                };
            }

            // Named addon found on disk but no wiki entry exists → show Discord link
            // so the user can find support/info for their mod.
            if (addonOnDisk != null && effectiveMod == null)
            {
                effectiveMod = new GameMod
                {
                    Name       = game.Name,
                    Status     = "💬",
                    DiscordUrl = "https://discord.gg/gF4GRJWZ2A",
                };
            }

            // Apply UE-Extended preference: if the game has it saved OR the file is on disk,
            // force the Mod URL to the marat569 source so the install button targets it.
            // Native HDR games always use UE-Extended, regardless of user toggle.
            // UE-Extended whitelist supersedes everything — hide Nexus link and force install/update/reinstall.
            bool isNativeHdr = IsNativeHdrGameMatch(game.Name);
            bool useUeExt = (addonOnDisk == UeExtendedFile)
                            || (IsUeExtendedGameMatch(game.Name) && (effectiveMod?.IsGenericUnreal == true || engine == EngineType.Unreal))
                            || (isNativeHdr && (effectiveMod?.IsGenericUnreal == true || engine == EngineType.Unreal));
            if (useUeExt && (effectiveMod?.IsGenericUnreal == true || engine == EngineType.Unreal))
            {
                // Create or override the mod to use UE-Extended URL
                effectiveMod = new GameMod
                {
                    Name            = effectiveMod?.Name ?? "Generic Unreal Engine",
                    Maintainer      = effectiveMod?.Maintainer ?? "ShortFuse",
                    SnapshotUrl     = UeExtendedUrl,
                    Status          = effectiveMod?.Status ?? "✅",
                    Notes           = effectiveMod?.Notes,
                    IsGenericUnreal = true,
                };
                // Persist preference if it was detected from disk or the game is native HDR
                if (addonOnDisk == UeExtendedFile || isNativeHdr)
                    _ueExtendedGames.Add(game.Name);
            }
            // UE-Extended whitelist games that have no engine detected — force them to use UE-Extended
            else if (useUeExt && effectiveMod == null)
            {
                effectiveMod = new GameMod
                {
                    Name            = "Generic Unreal Engine",
                    Maintainer      = "ShortFuse",
                    SnapshotUrl     = UeExtendedUrl,
                    Status          = "✅",
                    IsGenericUnreal = true,
                };
                fallback = effectiveMod;
                if (isNativeHdr)
                    _ueExtendedGames.Add(game.Name);
            }

            // UE-Extended whitelist supersedes Nexus/Discord external links — force installable
            if (useUeExt && effectiveMod != null)
            {
                // Strip Nexus/Discord links so the card shows install/update/reinstall buttons
                effectiveMod.NexusUrl   = null;
                effectiveMod.DiscordUrl = null;
            }

            // Look up aux records for this game
            var dcRec = auxRecords.FirstOrDefault(r =>
                r.GameName.Equals(game.Name, StringComparison.OrdinalIgnoreCase) &&
                r.AddonType == AuxInstallService.TypeDc);
            var rsRec = auxRecords.FirstOrDefault(r =>
                r.GameName.Equals(game.Name, StringComparison.OrdinalIgnoreCase) &&
                r.AddonType == AuxInstallService.TypeReShade);

            // ── Disk detection for ReShade & Display Commander ────────────────────
            // If no DB record exists, scan disk for the known filenames so that
            // manually installed or previously installed instances are shown correctly.
            //
            // dxgi.dll is AMBIGUOUS — both ReShade (DC Mode OFF) and DC (DC Mode ON) use it.
            // We use strict positive identification (binary string scan + exact size match
            // against staged/cached copies) to classify it. Files that cannot be positively
            // identified as ReShade or DC are left alone — no record is created.
            if (rsRec == null)
            {
                // ReShade64.dll (DC Mode ON, 64-bit) is unambiguous — always ReShade.
                var rs64Path = Path.Combine(installPath, AuxInstallService.RsDcModeName);
                if (File.Exists(rs64Path))
                {
                    rsRec = new AuxInstalledRecord
                    {
                        GameName    = game.Name,
                        InstallPath = installPath,
                        AddonType   = AuxInstallService.TypeReShade,
                        InstalledAs = AuxInstallService.RsDcModeName,
                        InstalledAt = File.GetLastWriteTimeUtc(rs64Path),
                    };
                }
                else
                {
                    // ReShade32.dll (DC Mode ON, 32-bit) is also unambiguous — always ReShade.
                    var rs32Path = Path.Combine(installPath, AuxInstallService.RsDcModeName32);
                    if (File.Exists(rs32Path))
                    {
                        rsRec = new AuxInstalledRecord
                        {
                            GameName    = game.Name,
                            InstallPath = installPath,
                            AddonType   = AuxInstallService.TypeReShade,
                            InstalledAs = AuxInstallService.RsDcModeName32,
                            InstalledAt = File.GetLastWriteTimeUtc(rs32Path),
                        };
                    }
                    else
                    {
                        // dxgi.dll — only attribute to ReShade if the file size matches ReShade
                        var dxgiPath = Path.Combine(installPath, AuxInstallService.RsNormalName);
                        if (File.Exists(dxgiPath) && AuxInstallService.IsReShadeFile(dxgiPath))
                        {
                            rsRec = new AuxInstalledRecord
                            {
                                GameName    = game.Name,
                                InstallPath = installPath,
                                AddonType   = AuxInstallService.TypeReShade,
                                InstalledAs = AuxInstallService.RsNormalName,
                                InstalledAt = File.GetLastWriteTimeUtc(dxgiPath),
                            };
                        }
                    }
                }
            }
            if (dcRec == null)
            {
                // zzz_display_commander.addon64 is unambiguous — always DC (normal mode).
                var dxgiPath = Path.Combine(installPath, AuxInstallService.DcNormalName);
                if (File.Exists(dxgiPath))
                {
                    dcRec = new AuxInstalledRecord
                    {
                        GameName    = game.Name,
                        InstallPath = installPath,
                        AddonType   = AuxInstallService.TypeDc,
                        InstalledAs = AuxInstallService.DcNormalName,
                        InstalledAt = File.GetLastWriteTimeUtc(dxgiPath),
                    };
                }
                else
                {
                    // dxgi.dll (DC Mode ON) — only attribute to DC if positively identified as DC.
                    // A dxgi.dll that is neither ReShade nor DC is foreign (e.g. DXVK, Special K) — leave it alone.
                    var dcDxgiPath = Path.Combine(installPath, AuxInstallService.DcDxgiName);
                    if (File.Exists(dcDxgiPath) && AuxInstallService.IsDcFileStrict(dcDxgiPath))
                    {
                        dcRec = new AuxInstalledRecord
                        {
                            GameName    = game.Name,
                            InstallPath = installPath,
                            AddonType   = AuxInstallService.TypeDc,
                            InstalledAs = AuxInstallService.DcDxgiName,
                            InstalledAt = File.GetLastWriteTimeUtc(dcDxgiPath),
                        };
                    }
                }
            }

            cards.Add(new GameCardViewModel
            {
                GameName               = game.Name,
                Mod                    = effectiveMod,
                DetectedGame           = game,
                InstallPath            = installPath,
                Source                 = game.Source,
                InstalledRecord        = record,
                Status                 = record != null ? GameStatus.Installed : GameStatus.Available,
                WikiStatus             = (_wikiExclusions.Contains(game.Name)
                                           || (effectiveMod?.SnapshotUrl == null && effectiveMod?.DiscordUrl != null && effectiveMod?.NexusUrl == null))
                                          ? "💬"
                                          : effectiveMod?.Status ?? "—",
                Maintainer             = effectiveMod?.Maintainer ?? "",
                IsGenericMod           = useUeExt || (fallback != null && mod == null),
                EngineHint             = (useUeExt && engine == EngineType.Unknown) ? "Unreal Engine"
                                       : engine == EngineType.Unreal       ? "Unreal Engine"
                                       : engine == EngineType.UnrealLegacy ? "Unreal (Legacy)"
                                       : engine == EngineType.Unity        ? "Unity" : "",
                Notes                  = effectiveMod != null ? BuildNotes(game.Name, effectiveMod, fallback, genericNotes, isNativeHdr) : "",
                InstalledAddonFileName = record?.AddonFileName,
                IsHidden               = _hiddenGames.Contains(game.Name),
                IsManuallyAdded        = game.IsManuallyAdded,
                UseUeExtended          = useUeExt,
                IsExternalOnly         = _wikiExclusions.Contains(game.Name)
                                         ? true
                                         : effectiveMod?.SnapshotUrl == null &&
                                           (effectiveMod?.NexusUrl != null || effectiveMod?.DiscordUrl != null),
                ExternalUrl            = _wikiExclusions.Contains(game.Name)
                                         ? "https://discord.gg/gF4GRJWZ2A"
                                         : effectiveMod?.NexusUrl ?? effectiveMod?.DiscordUrl ?? "",
                ExternalLabel          = _wikiExclusions.Contains(game.Name)
                                         ? "Get on Discord"
                                         : effectiveMod?.NexusUrl != null ? "Get on Nexus Mods" : "Get on Discord",
                NexusUrl               = effectiveMod?.NexusUrl,
                DiscordUrl             = _wikiExclusions.Contains(game.Name)
                                         ? "https://discord.gg/gF4GRJWZ2A"
                                         : effectiveMod?.DiscordUrl,
                NameUrl                = effectiveMod?.NameUrl,
                DcModeExcluded         = _dcModeExcludedGames.Contains(game.Name),
                ExcludeFromUpdateAll   = _updateAllExcludedGames.Contains(game.Name),
                ExcludeFromShaders     = _shaderExcludedGames.Contains(game.Name),
                Is32Bit                = _is32BitGames.Contains(game.Name),
                IsNativeHdrGame        = IsNativeHdrGameMatch(game.Name),
                DcRecord               = dcRec,
                DcStatus               = dcRec != null ? GameStatus.Installed : GameStatus.NotInstalled,
                DcInstalledFile        = dcRec?.InstalledAs,
                RsRecord               = rsRec,
                RsStatus               = rsRec != null ? GameStatus.Installed : GameStatus.NotInstalled,
                RsInstalledFile        = rsRec?.InstalledAs,
            });
        }
        ApplyCardOverrides(cards);
        return cards;
    }

    private static string BuildNotes(string gameName, GameMod effectiveMod, GameMod? fallback, Dictionary<string, string> genericNotes, bool isNativeHdr = false)
    {
        // Specific mod — wiki tooltip note (may be null/empty if no tooltip)
        if (fallback == null) return effectiveMod.Notes ?? "";

        var parts = new List<string>();

        if (isNativeHdr)
        {
            // Native HDR / UE-Extended whitelisted games — don't show generic UE info
            parts.Add("⚠ In-game HDR must be turned ON for UE-Extended to work correctly in this title.");

            var specific = GetGenericNote(gameName, genericNotes);
            if (!string.IsNullOrEmpty(specific))
            {
                parts.Add("\n📋 Game-specific settings:");
                parts.Add(specific);
            }
        }
        else if (effectiveMod.IsGenericUnreal)
        {
            var specific = GetGenericNote(gameName, genericNotes);
            if (!string.IsNullOrEmpty(specific))
            {
                parts.Add("📋 Game-specific settings:");
                parts.Add(specific);
            }
            parts.Add(UnrealWarnings);
        }
        else // Unity
        {
            var specific = GetGenericNote(gameName, genericNotes);
            if (!string.IsNullOrEmpty(specific))
            {
                parts.Add("📋 Game-specific settings:");
                parts.Add(specific);
            }
        }

        return string.Join("\n", parts);
    }

    private static string? ScanForInstalledAddon(string installPath, GameMod? mod)
    {
        if (!Directory.Exists(installPath)) return null;
        try
        {
            if (mod?.AddonFileName != null && File.Exists(Path.Combine(installPath, mod.AddonFileName)))
                return mod.AddonFileName;
            // First try direct files in the folder
            foreach (var ext in new[] { "*.addon64", "*.addon32" })
            {
                var found = Directory.GetFiles(installPath, ext)
                    .FirstOrDefault(f => Path.GetFileName(f).StartsWith("renodx", StringComparison.OrdinalIgnoreCase));
                if (found != null) return Path.GetFileName(found);
            }

            // Search common subdirectories (Binaries/Win64, Binaries/Win32) and fallback to a limited recursive search
            var commonPaths = new[] { "Binaries\\Win64", "Binaries\\Win32", "Binaries\\x86", "x64", "x86" };
            foreach (var sub in commonPaths)
            {
                try
                {
                    var sp = Path.Combine(installPath, sub);
                    if (!Directory.Exists(sp)) continue;
                    foreach (var ext in new[] { "*.addon64", "*.addon32" })
                    {
                        var found = Directory.GetFiles(sp, ext)
                            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("renodx", StringComparison.OrdinalIgnoreCase));
                        if (found != null) return Path.GetFileName(found);
                    }
                }
                catch { }
            }

            // Last resort: limited recursive search (catch and ignore access issues)
            try
            {
                foreach (var ext in new[] { "*.addon64", "*.addon32" })
                {
                    var found = Directory.EnumerateFiles(installPath, ext, SearchOption.AllDirectories)
                        .FirstOrDefault(f => Path.GetFileName(f).StartsWith("renodx", StringComparison.OrdinalIgnoreCase));
                    if (found != null) return Path.GetFileName(found);
                }
            }
            catch { /* ignore permission errors */ }
        }
        catch { }
        return null;
    }

    private void ApplyFilter()
    {
        var query = SearchQuery.Trim().ToLowerInvariant();
        var filtered = _allCards.Where(c =>
        {
            // Search match first
            var matchSearch = string.IsNullOrEmpty(query)
                || c.GameName.ToLowerInvariant().Contains(query)
                || c.Maintainer.ToLowerInvariant().Contains(query);
            if (!matchSearch) return false;

            // Hidden tab always shows hidden games regardless of the ShowHidden toggle
            if (FilterMode == "Hidden") return c.IsHidden;

            // Engine filters
            if (FilterMode == "Unity")
            {
                // match cards detected as Unity (EngineHint) or generic Unity mod
                var isUnity = (!string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0)
                              || (c.Mod?.IsGenericUnity == true);
                if (!isUnity) return false;
                // hide hidden games on non-hidden tabs
                return !c.IsHidden;
            }
            if (FilterMode == "Unreal")
            {
                var isUnreal = (!string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) >= 0)
                              || (c.Mod?.IsGenericUnreal == true);
                if (!isUnreal) return false;
                return !c.IsHidden;
            }
            if (FilterMode == "Other")
            {
                var isUnity = (!string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0)
                              || (c.Mod?.IsGenericUnity == true);
                var isUnreal = (!string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) >= 0)
                              || (c.Mod?.IsGenericUnreal == true);
                if (isUnity || isUnreal) return false;
                return !c.IsHidden;
            }

            // For Installed tab, the ShowHidden toggle controls whether hidden installed games are included
            if (FilterMode == "Installed")
            {
                var isInstalled = c.Status == GameStatus.Installed || c.Status == GameStatus.UpdateAvailable;
                if (!isInstalled) return false;
                return ShowHidden || !c.IsHidden;
            }

            // Not Installed: non-hidden games without RenoDX mod installed
            if (FilterMode == "NotInstalled")
            {
                if (c.IsHidden) return false;
                return c.Status != GameStatus.Installed && c.Status != GameStatus.UpdateAvailable;
            }

            // Default: hide hidden games (they belong in Hidden tab)
            if (c.IsHidden) return false;
            return true;
        }).ToList();

        DisplayedGames.Clear();
        foreach (var c in filtered) DisplayedGames.Add(c);
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        InstalledCount = _allCards.Count(c => c.Status == GameStatus.Installed || c.Status == GameStatus.UpdateAvailable);
        HiddenCount    = _allCards.Count(c => c.IsHidden);
        TotalGames     = DisplayedGames.Count;
        OnPropertyChanged(nameof(InstalledCount));
        OnPropertyChanged(nameof(TotalGames));
        OnPropertyChanged(nameof(HiddenCount));
    }

    public void SaveLibraryPublic() => SaveLibrary();
    private void SaveLibrary()
    {
        var detectedGames = _allCards
            .Where(c => !c.IsManuallyAdded && c.DetectedGame != null)
            .Select(c => c.DetectedGame!)
            .ToList();

        // Build addon cache safely — multiple DLC cards can share the same install path,
        // so use a plain dict with [] assignment instead of ToDictionary (which throws on dupes).
        var addonCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in _allCards.Where(c => !string.IsNullOrEmpty(c.InstallPath)))
            addonCache[c.InstallPath.ToLowerInvariant()] = !string.IsNullOrEmpty(c.InstalledAddonFileName);

        GameLibraryService.Save(detectedGames, addonCache, _hiddenGames, _manualGames);
    }

    private static string FormatAge(DateTime utc)
    {
        var age = DateTime.UtcNow - utc;
        if (age.TotalMinutes < 1) return "just now";
        if (age.TotalHours   < 1) return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalDays    < 1) return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }

    partial void OnSearchQueryChanged(string v)  => ApplyFilter();
    partial void OnShowHiddenChanged(bool v)     => ApplyFilter();
}


