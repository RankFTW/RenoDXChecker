using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RenoDXChecker.Models;
using RenoDXChecker.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace RenoDXChecker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly HttpClient _http;
    private readonly ModInstallService _installer;
    private List<GameMod> _allMods = new();
    private Dictionary<string, string> _genericNotes = new(StringComparer.OrdinalIgnoreCase);
    private List<GameCardViewModel> _allCards = new();
    private List<DetectedGame> _manualGames = new();
    private HashSet<string> _hiddenGames = new(StringComparer.OrdinalIgnoreCase);

    // Settings stored as JSON — ApplicationData.Current throws in unpackaged WinUI 3
    private static readonly string _settingsFilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXChecker", "settings.json");

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
        _http.DefaultRequestHeaders.Add("User-Agent", "RenoDXChecker/2.0");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _installer = new ModInstallService(_http);
        // Subscribe to installer events — on install we'll perform a full refresh
        _installer.InstallCompleted += Installer_InstallCompleted;
        LoadNameMappings();
        LoadThemeAndDensity();
    }

    // --- persisted settings: name mappings, theme, density ---
    private Dictionary<string, string> _nameMappings = new(StringComparer.OrdinalIgnoreCase);
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
        }
        catch { _nameMappings = new(StringComparer.OrdinalIgnoreCase); }
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
        // Remove both exact and any normalised-key match
        _nameMappings.Remove(detectedName);
        var norm = GameDetectionService.NormalizeName(detectedName);
        var toRemove = _nameMappings.Keys
            .Where(k => GameDetectionService.NormalizeName(k) == norm).ToList();
        foreach (var k in toRemove) _nameMappings.Remove(k);
        SaveNameMappings();
        // Rebuild cards so removal takes effect immediately
        DispatcherQueue?.TryEnqueue(() => { _ = InitializeAsync(forceRescan: false); });
    }

    private void SaveNameMappings()
    {
        try
        {
            var s = LoadSettingsFile();
            s["NameMappings"] = JsonSerializer.Serialize(_nameMappings);
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
        s = s.Replace("™", "").Replace("®", "");
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

    private void Installer_InstallCompleted(InstalledModRecord record)
    {
        DispatcherQueue?.TryEnqueue(() => { _ = InitializeAsync(forceRescan: true); });
    }

    // Debug logging removed; full rescan is the canonical refresh path.

    // (Targeted refresh helpers removed) Full rescan (`RefreshAsync` / `InitializeAsync(forceRescan:true`) is the single source of truth.

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
                SnapshotUrl   = effectiveMod?.SnapshotUrl,
            };
            _installer.SaveRecordPublic(record);
        }

        // Named addon found on disk but no wiki entry → show Discord link
        if (addonOnDisk != null && effectiveMod == null)
        {
            effectiveMod = new GameMod
            {
                Name       = game.Name,
                Status     = "✅",
                DiscordUrl = "https://discord.gg/gF4GRJWZ2A",
            };
        }

        var card = new GameCardViewModel
        {
            GameName       = game.Name,
            Mod            = effectiveMod,
            DetectedGame   = game,
            InstallPath    = scanPath,
            Source         = "Manual",
            InstalledRecord = record,
            Status         = record != null ? GameStatus.Installed : GameStatus.Available,
            WikiStatus     = effectiveMod?.Status ?? "—",
            Maintainer     = effectiveMod?.Maintainer ?? "",
            IsGenericMod   = fallback != null && mod == null,
            EngineHint     = engine == EngineType.Unreal       ? "Unreal Engine"
                           : engine == EngineType.UnrealLegacy ? "Unreal (Legacy)"
                           : engine == EngineType.Unity        ? "Unity" : "",
            Notes          = effectiveMod != null ? BuildNotes(game.Name, effectiveMod, fallback, _genericNotes) : "",
            InstalledAddonFileName = record?.AddonFileName,
            IsExternalOnly = effectiveMod?.SnapshotUrl == null &&
                             (effectiveMod?.NexusUrl != null || effectiveMod?.DiscordUrl != null),
            ExternalUrl    = effectiveMod?.NexusUrl ?? effectiveMod?.DiscordUrl ?? "",
            ExternalLabel  = effectiveMod?.NexusUrl != null ? "Get on Nexus Mods" : "Get on Discord",
            NexusUrl       = effectiveMod?.NexusUrl,
            DiscordUrl     = effectiveMod?.DiscordUrl,
            NameUrl        = effectiveMod?.NameUrl,
            IsManuallyAdded = true,
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

            // Update just this card in-place — no full rescan needed.
            // The record is already complete; push it into the card's observable
            // properties and let bindings update the UI instantly.
            DispatcherQueue?.TryEnqueue(() =>
            {
                card.InstalledRecord        = record;
                card.InstalledAddonFileName = record.AddonFileName;
                card.Status                 = GameStatus.Installed;
                card.ActionMessage          = "✅ Installed! Press Home in-game to open ReShade.";
                CrashReporter.Log($"Install complete: {card.GameName} — {record.AddonFileName}");
                card.NotifyAll();
                SaveLibrary();
                UpdateCounts();
            });
        }
        catch (Exception ex)
        {
            card.ActionMessage = $"❌ Failed: {ex.Message}";
            CrashReporter.WriteCrashReport("InstallModAsync", ex, note: $"Game: {card.GameName}, Path: {card.InstallPath}");
        }
        finally { card.IsInstalling = false; }
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
                SubStatusText = "Fetching latest mod info...";
                detectedGames = GameLibraryService.ToDetectedGames(savedLib);
                addonCache    = savedLib.AddonScanCache;
                var wr = await WikiService.FetchAllAsync(_http);
                _allMods      = wr.Mods;
                _genericNotes = wr.GenericNotes;
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

            var records = _installer.LoadAll();
            SubStatusText = "Matching mods and checking install status...";
            CrashReporter.Log($"Building cards for {allGames.Count} games...");
            _allCards = await Task.Run(() => BuildCards(allGames, records, addonCache, _genericNotes));
            CrashReporter.Log($"BuildCards complete: {_allCards.Count} cards.");

            // Check for updates (async, parallel, non-blocking)
            CrashReporter.Log("Starting background update checks...");
            _ = Task.Run(() => CheckForUpdatesAsync(_allCards, records));

            _allCards = _allCards.OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase).ToList();
            SaveLibrary();
            UpdateCounts();
            ApplyFilter();

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

    private async Task CheckForUpdatesAsync(List<GameCardViewModel> cards, List<InstalledModRecord> records)
    {
        var installed = cards
            .Where(c => c.Status == GameStatus.Installed && c.InstalledRecord?.SnapshotUrl != null)
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
            else if (!record.RemoteFileSize.HasValue)
            {
                // Migration: record didn't have RemoteFileSize (installed before v1.0.2).
                // CheckForUpdateAsync already did a HEAD — grab the size from the local file
                // as a reasonable baseline until the next real install records the true value.
                var localFile = Path.Combine(record.InstallPath, record.AddonFileName);
                if (File.Exists(localFile))
                {
                    record.RemoteFileSize = new FileInfo(localFile).Length;
                    _installer.SaveRecordPublic(record);
                }
            }
        });

        await Task.WhenAll(tasks);
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

        return byPath;
    }

    // ── Card building ─────────────────────────────────────────────────────────────

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
        Dictionary<string, bool> addonCache,
        Dictionary<string, string> genericNotes)
    {
        var cards = new List<GameCardViewModel>();
        var genericUnreal = MakeGenericUnreal();
        var genericUnity  = MakeGenericUnity();

        var gameInfos = detectedGames.AsParallel().Select(game =>
        {
            var (installPath, engine) = GameDetectionService.DetectEngineAndPath(game.InstallPath);
            var mod      = GameDetectionService.MatchGame(game, _allMods, _nameMappings);
            // UnrealLegacy (UE3 and below) cannot use the RenoDX addon system — no fallback mod offered.
            var fallback = mod == null ? (engine == EngineType.Unreal      ? genericUnreal
                                        : engine == EngineType.Unity       ? genericUnity : null) : null;
            return (game, installPath, engine, mod, fallback);
        }).ToList();

        foreach (var (game, installPath, engine, mod, fallback) in gameInfos)
        {
            // Always show every detected game — even if no wiki mod exists.
            // The card will have no install button if there's no snapshot URL,
            // but a RenoDX addon already on disk will still be detected and shown.
            var effectiveMod = mod ?? fallback; // may be null for truly unknown games

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
                record = new InstalledModRecord
                {
                    GameName      = game.Name,
                    InstallPath   = installPath,
                    AddonFileName = addonOnDisk,
                    InstalledAt   = File.GetLastWriteTimeUtc(Path.Combine(installPath, addonOnDisk)),
                    SnapshotUrl   = effectiveMod?.SnapshotUrl,
                };
                _installer.SaveRecordPublic(record);
            }

            // Named addon found on disk but no wiki entry exists → show Discord link
            // so the user can find support/info for their mod.
            if (addonOnDisk != null && effectiveMod == null)
            {
                effectiveMod = new GameMod
                {
                    Name       = game.Name,
                    Status     = "✅",
                    DiscordUrl = "https://discord.gg/gF4GRJWZ2A",
                };
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
                WikiStatus             = effectiveMod?.Status ?? "—",
                Maintainer             = effectiveMod?.Maintainer ?? "",
                IsGenericMod           = fallback != null && mod == null,
                EngineHint             = engine == EngineType.Unreal       ? "Unreal Engine"
                                       : engine == EngineType.UnrealLegacy ? "Unreal (Legacy)"
                                       : engine == EngineType.Unity        ? "Unity" : "",
                Notes                  = effectiveMod != null ? BuildNotes(game.Name, effectiveMod, fallback, genericNotes) : "",
                InstalledAddonFileName = record?.AddonFileName,
                IsHidden               = _hiddenGames.Contains(game.Name),
                IsManuallyAdded        = game.IsManuallyAdded,
                IsExternalOnly         = effectiveMod?.SnapshotUrl == null &&
                                         (effectiveMod?.NexusUrl != null || effectiveMod?.DiscordUrl != null),
                ExternalUrl            = effectiveMod?.NexusUrl ?? effectiveMod?.DiscordUrl ?? "",
                ExternalLabel          = effectiveMod?.NexusUrl != null ? "Get on Nexus Mods" : "Get on Discord",
                NexusUrl               = effectiveMod?.NexusUrl,
                DiscordUrl             = effectiveMod?.DiscordUrl,
                NameUrl                = effectiveMod?.NameUrl,
            });
        }
        return cards;
    }

    private static string BuildNotes(string gameName, GameMod effectiveMod, GameMod? fallback, Dictionary<string, string> genericNotes)
    {
        // Specific mod — wiki tooltip note (may be null/empty if no tooltip)
        if (fallback == null) return effectiveMod.Notes ?? "";

        var parts = new List<string>();

        if (effectiveMod.IsGenericUnreal)
        {
            parts.Add("This game uses the Generic Unreal Engine plugin.");
            parts.Add("📁 Install the .addon64 file next to the *-Win64-Shipping.exe");
            parts.Add("   (usually GameName\\Binaries\\Win64, NOT in the Engine folder)\n");

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
            parts.Add("This game uses the Generic Unity Engine plugin.");
            parts.Add("📁 Install ReShade next to UnityPlayer.dll (usually the game root folder).");
            parts.Add("   Two versions available — use 64-bit unless your game is 32-bit.\n");
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
