using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RenoDXChecker.Models;
using RenoDXChecker.Services;

namespace RenoDXChecker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly HttpClient _http;
    private readonly ModInstallService _installer;
    private List<GameMod> _allMods = new();
    private Dictionary<string, string> _genericNotes = new(StringComparer.OrdinalIgnoreCase);
    private List<GameCardViewModel> _allCards = new();

    
    
    // Warnings shown for ALL Unreal Engine generic mod games (from wiki)
    private const string UnrealWarnings =
        "⚠ UNREAL ENGINE MOD WARNINGS\n\n" +
        "🖥 Black Screen on Launch\n" +
        "Upgrade `R10G10B10A2_UNORM` → `output size`\n\n" +
        "🔓 Unlock upgrade sliders by switching Settings Mode from Simple → Advanced.\n" +
        "Then restart the game for the upgrade to take effect.\n\n" +
        "🖥 DLSS FG not working properly (Flickering or other odd behaviour)\n" +
        "Replace DLSSG DLL to the older 3.8.x version (locks to FG x2) or use DLSS FIX (beta) on the Discord.";

    private static string BuildNotes(string gameName, GameMod effectiveMod, GameMod? fallback, Dictionary<string, string> genericNotes)
    {
        // For specific mods — use the wiki status tooltip note
        if (fallback == null) return effectiveMod.Notes ?? "";

        var parts = new List<string>();

        if (effectiveMod.IsGenericUnreal)
        {
            parts.Add("This game uses the Generic Unreal Engine plugin.");
            parts.Add("Install the .addon64 file next to the *-Win64-Shipping.exe (usually Binaries\\Win64).\n");
            // Per-game specific note from wiki table
            if (genericNotes.TryGetValue(gameName, out var specific) && !string.IsNullOrEmpty(specific))
                parts.Add(specific + "\n");
            parts.Add(UnrealWarnings);
        }
        else
        {
            parts.Add("This game uses the Generic Unity Engine plugin.");
            parts.Add("Install ReShade next to UnityPlayer.dll (usually the game root folder).\n");
            parts.Add("Two versions are available — install 64-bit unless your game is 32-bit.");
            if (genericNotes.TryGetValue(gameName, out var specific) && !string.IsNullOrEmpty(specific))
                parts.Add("\nGame-specific notes:\n" + specific);
        }

        return string.Join("\n", parts);
    }

    public MainViewModel()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "RenoDXChecker/2.0");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _installer = new ModInstallService(_http);
    }

    [ObservableProperty] private string _statusText = "Loading...";
    [ObservableProperty] private string _subStatusText = "";
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private string _filterMode = "Detected";
    [ObservableProperty] private int _totalGames;
    [ObservableProperty] private int _installedCount;

    public ObservableCollection<GameCardViewModel> DisplayedGames { get; } = new();

    [RelayCommand] public void SetFilter(string filter) { FilterMode = filter; ApplyFilter(); }

    [RelayCommand]
    public async Task InstallModAsync(GameCardViewModel? card)
    {
        if (card?.Mod?.SnapshotUrl == null) return;
        if (string.IsNullOrEmpty(card.InstallPath))
        {
            card.ActionMessage = "No install path set — use the folder button to pick the game folder.";
            return;
        }
        card.IsInstalling = true;
        card.ActionMessage = "Starting download...";
        try
        {
            var progress = new Progress<(string msg, double pct)>(p =>
            {
                card.ActionMessage   = p.msg;
                card.InstallProgress = p.pct;
            });
            var record = await _installer.InstallAsync(card.Mod, card.InstallPath, progress);
            card.InstalledRecord       = record;
            card.InstalledAddonFileName = record.AddonFileName;
            card.Status                = GameStatus.Installed;
            card.ActionMessage         = "✅ Installed! Launch game and press Home to open ReShade.";
            UpdateCounts();
        }
        catch (Exception ex) { card.ActionMessage = $"❌ Failed: {ex.Message}"; }
        finally { card.IsInstalling = false; }
    }

    [RelayCommand]
    public async Task InstallMod32Async(GameCardViewModel? card)
    {
        if (card?.Mod?.SnapshotUrl32 == null) return;
        // Temporarily swap URL to the 32-bit one, install, then restore
        var orig = card.Mod.SnapshotUrl;
        card.Mod.SnapshotUrl = card.Mod.SnapshotUrl32;
        await InstallModAsync(card);
        card.Mod.SnapshotUrl = orig;
    }

    [RelayCommand]
    public void UninstallMod(GameCardViewModel? card)
    {
        if (card?.InstalledRecord == null) return;
        _installer.Uninstall(card.InstalledRecord);
        card.InstalledRecord        = null;
        card.InstalledAddonFileName = null;
        card.Status                 = GameStatus.Available;
        card.ActionMessage          = "Mod uninstalled.";
        UpdateCounts();
    }

    // ── Init ──────────────────────────────────────────────────────────────────────

    public async Task InitializeAsync(bool forceRescan = false)
    {
        IsLoading = true;
        DisplayedGames.Clear();
        _allCards.Clear();

        try
        {
            var savedLib = GameLibraryService.Load();
            List<DetectedGame> detectedGames;
            Dictionary<string, bool> addonCache;

            if (savedLib != null && !forceRescan)
            {
                StatusText    = $"Library loaded ({savedLib.Games.Count} games, scanned {FormatAge(savedLib.LastScanned)})";
                SubStatusText = "Fetching latest mod info...";
                detectedGames = GameLibraryService.ToDetectedGames(savedLib);
                addonCache    = savedLib.AddonScanCache;
                var wikiResult = await WikiService.FetchAllAsync(_http);
                _allMods       = wikiResult.Mods;
                _genericNotes  = wikiResult.GenericNotes;
            }
            else
            {
                StatusText    = "Scanning game library...";
                SubStatusText = "Running store scans + wiki fetch simultaneously...";
                var wikiTask   = WikiService.FetchAllAsync(_http);
                var detectTask = Task.Run(DetectAllGamesDeduped);
                await Task.WhenAll(wikiTask, detectTask);
                _allMods       = wikiTask.Result.Mods;
                _genericNotes  = wikiTask.Result.GenericNotes;
                detectedGames = detectTask.Result;
                addonCache    = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                // Fresh scan — addon cache will be populated during BuildCards
            }

            var records = _installer.LoadAll();
            SubStatusText = "Matching mods and checking install status...";
            _allCards = await Task.Run(() => BuildCards(detectedGames, records, addonCache, _genericNotes));
            _allCards = _allCards.OrderByDescending(c => (int)c.Status).ThenBy(c => c.GameName).ToList();

            // Save updated library + addon cache
            GameLibraryService.Save(detectedGames, addonCache);

            UpdateCounts();
            ApplyFilter();

            StatusText    = $"{detectedGames.Count} games detected · {InstalledCount} mods installed";
            SubStatusText = "";
        }
        catch (Exception ex) { StatusText = "Error loading"; SubStatusText = ex.Message; }
        finally { IsLoading = false; }
    }

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
        return tasks.SelectMany(t => t.Result)
            .GroupBy(g => GameDetectionService.NormalizeName(g.Name))
            .Select(grp => grp
                .GroupBy(g => g.InstallPath.TrimEnd('\\', '/').ToLowerInvariant())
                .Select(pg => pg.First()).First())
            .ToList();
    }

    // ── Card building ─────────────────────────────────────────────────────────────

    private List<GameCardViewModel> BuildCards(
        List<DetectedGame> detectedGames,
        List<InstalledModRecord> records,
        Dictionary<string, bool> addonCache,
        Dictionary<string, string> genericNotes)
    {
        var cards = new List<GameCardViewModel>();

        var genericUnreal = new GameMod { Name = "Generic Unreal Engine", Maintainer = "ShortFuse",
            SnapshotUrl = WikiService.GenericUnrealUrl, Status = "✅", IsGenericUnreal = true, Notes = null };
        var genericUnity  = new GameMod { Name = "Generic Unity Engine",  Maintainer = "ShortFuse",
            SnapshotUrl   = WikiService.GenericUnityUrl64,
            SnapshotUrl32 = WikiService.GenericUnityUrl32,
            Status = "✅", IsGenericUnity = true, Notes = null };

        var gameInfos = detectedGames.AsParallel().Select(game =>
        {
            var (installPath, engine) = GameDetectionService.DetectEngineAndPath(game.InstallPath);
            var mod      = GameDetectionService.MatchGame(game, _allMods);
            var fallback = mod == null
                ? engine == EngineType.Unreal ? genericUnreal
                : engine == EngineType.Unity  ? genericUnity : null
                : null;
            return (game, installPath, engine, mod, fallback);
        }).ToList();

        foreach (var (game, installPath, engine, mod, fallback) in gameInfos)
        {
            if (mod == null && fallback == null) continue;

            var effectiveMod = mod ?? fallback!;
            var record = records.FirstOrDefault(r =>
                r.GameName.Equals(game.Name, StringComparison.OrdinalIgnoreCase));

            // Addon disk scan — use cache to avoid rescanning every launch
            string? addonOnDisk = null;
            var cacheKey = installPath.ToLowerInvariant();
            if (addonCache.ContainsKey(cacheKey))
            {
                // Use cached result — only re-scan if cache says installed (verify file still there)
                if (addonCache[cacheKey])
                    addonOnDisk = ScanForInstalledAddon(installPath, effectiveMod);
            }
            else
            {
                // Not in cache yet — scan and cache result
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
                    SnapshotUrl   = effectiveMod.SnapshotUrl,
                };
                _installer.SaveRecordPublic(record);
            }

            cards.Add(new GameCardViewModel
            {
                GameName              = game.Name,
                Mod                   = effectiveMod,
                DetectedGame          = game,
                InstallPath           = installPath,
                Source                = game.Source,
                InstalledRecord       = record,
                Status                = record != null ? GameStatus.Installed : GameStatus.Available,
                WikiStatus            = effectiveMod.Status,
                Maintainer            = effectiveMod.Maintainer,
                IsGenericMod          = fallback != null && mod == null,
                EngineHint            = engine == EngineType.Unreal ? "Unreal Engine"
                                      : engine == EngineType.Unity  ? "Unity" : "",
                Notes                 = BuildNotes(game.Name, effectiveMod, fallback, genericNotes),
                InstalledAddonFileName = record?.AddonFileName,
                IsExternalOnly        = effectiveMod.SnapshotUrl == null &&
                                        (effectiveMod.NexusUrl != null || effectiveMod.DiscordUrl != null),
                ExternalUrl           = effectiveMod.NexusUrl ?? effectiveMod.DiscordUrl ?? "",
                ExternalLabel         = effectiveMod.NexusUrl != null ? "Get on Nexus Mods" : "Get on Discord",
            });
        }
        return cards;
    }

    private static string? ScanForInstalledAddon(string installPath, GameMod mod)
    {
        if (!Directory.Exists(installPath)) return null;
        try
        {
            if (mod.AddonFileName != null && File.Exists(Path.Combine(installPath, mod.AddonFileName)))
                return mod.AddonFileName;
            foreach (var ext in new[] { "*.addon64", "*.addon32" })
            {
                var found = Directory.GetFiles(installPath, ext)
                    .FirstOrDefault(f => Path.GetFileName(f).StartsWith("renodx", StringComparison.OrdinalIgnoreCase));
                if (found != null) return Path.GetFileName(found);
            }
        }
        catch { }
        return null;
    }

    private void ApplyFilter()
    {
        var query = SearchQuery.Trim().ToLowerInvariant();
        var filtered = _allCards.Where(c =>
        {
            var matchSearch = string.IsNullOrEmpty(query)
                || c.GameName.ToLowerInvariant().Contains(query)
                || c.Maintainer.ToLowerInvariant().Contains(query);
            var matchFilter = FilterMode switch
            {
                "Installed" => c.Status == GameStatus.Installed,
                _           => true
            };
            return matchSearch && matchFilter;
        }).ToList();

        DisplayedGames.Clear();
        foreach (var c in filtered) DisplayedGames.Add(c);
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        InstalledCount = _allCards.Count(c => c.Status == GameStatus.Installed);
        TotalGames     = DisplayedGames.Count;
        OnPropertyChanged(nameof(InstalledCount));
        OnPropertyChanged(nameof(TotalGames));
    }

    private static string FormatAge(DateTime utc)
    {
        var age = DateTime.UtcNow - utc;
        if (age.TotalMinutes < 1) return "just now";
        if (age.TotalHours  < 1) return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalDays   < 1) return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }

    partial void OnSearchQueryChanged(string v) => ApplyFilter();
}
