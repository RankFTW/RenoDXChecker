using System.Reflection;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for DC file rename on mode switch.
/// Verifies that ApplyDcModeSwitchForCard correctly renames the DC file
/// when switching to/from DC Mode Custom (level 3).
///
/// Feature: dc-mode-ui-enhancements
/// Validates: Requirements 4.7, 4.8, 4.9
/// </summary>
public class DcFileRenameOnModeSwitchTests : IDisposable
{
    private readonly string _tempRoot;

    public DcFileRenameOnModeSwitchTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcRename_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    /// <summary>
    /// When switching to DC Mode Custom (level 3) with a custom DLL filename set,
    /// the DC file should be renamed to the custom filename.
    /// Validates: Requirement 4.7
    /// </summary>
    [Fact]
    public void SwitchToDcModeCustom_WithCustomFilename_RenamesDcFile()
    {
        // Arrange
        var gameName = "CustomRenameGame_" + Guid.NewGuid().ToString("N")[..6];
        var customDll = "d3d11.dll";
        var vm = CreateViewModel();
        vm.SetDcCustomDllFileName(gameName, customDll);
        vm.DcModeLevel = 0;

        var card = CreateCard(gameName, perGameDcMode: 3, dcInstalled: true,
            installedAs: AuxInstallService.DcDxgiName);
        InjectCards(vm, new List<GameCardViewModel> { card });

        // Act: switch from mode 1 to mode 3 (DC Mode Custom)
        vm.ApplyDcModeSwitchForCard(gameName, 1);

        // Assert: DC file renamed to custom filename
        Assert.Equal(customDll, card.DcRecord!.InstalledAs);
        Assert.True(File.Exists(Path.Combine(card.InstallPath!, customDll)));
        Assert.False(File.Exists(Path.Combine(card.InstallPath!, AuxInstallService.DcDxgiName)));
    }

    /// <summary>
    /// When switching to DC Mode Custom (level 3) with an empty custom DLL filename,
    /// the DC file should fall back to the default DC name.
    /// Validates: Requirement 4.8
    /// </summary>
    [Fact]
    public void SwitchToDcModeCustom_WithEmptyFilename_FallsBackToDefault()
    {
        // Arrange
        var gameName = "FallbackGame_" + Guid.NewGuid().ToString("N")[..6];
        var vm = CreateViewModel();
        // No custom DLL filename set (empty/null)
        vm.DcModeLevel = 0;

        var card = CreateCard(gameName, perGameDcMode: 3, dcInstalled: true,
            installedAs: AuxInstallService.DcDxgiName);
        InjectCards(vm, new List<GameCardViewModel> { card });

        // Act: switch from mode 1 to mode 3 with no custom filename
        vm.ApplyDcModeSwitchForCard(gameName, 1);

        // Assert: DC file renamed to default name (zzz_display_commander.addon64)
        Assert.Equal(AuxInstallService.DcNormalName, card.DcRecord!.InstalledAs);
        Assert.True(File.Exists(Path.Combine(card.InstallPath!, AuxInstallService.DcNormalName)));
        Assert.False(File.Exists(Path.Combine(card.InstallPath!, AuxInstallService.DcDxgiName)));
    }

    /// <summary>
    /// When switching from DC Mode Custom (level 3) to DC Mode 1,
    /// the DC file should be renamed back to dxgi.dll.
    /// Validates: Requirement 4.9
    /// </summary>
    [Fact]
    public void SwitchFromDcModeCustom_ToMode1_RenamesBackToDxgi()
    {
        // Arrange
        var gameName = "Mode1Game_" + Guid.NewGuid().ToString("N")[..6];
        var customDll = "d3d11.dll";
        var vm = CreateViewModel();
        vm.SetDcCustomDllFileName(gameName, customDll);
        vm.DcModeLevel = 0;

        var card = CreateCard(gameName, perGameDcMode: 1, dcInstalled: true,
            installedAs: customDll);
        InjectCards(vm, new List<GameCardViewModel> { card });

        // Act: switch from mode 3 (custom) to mode 1
        vm.ApplyDcModeSwitchForCard(gameName, 3);

        // Assert: DC file renamed to dxgi.dll
        Assert.Equal(AuxInstallService.DcDxgiName, card.DcRecord!.InstalledAs);
        Assert.True(File.Exists(Path.Combine(card.InstallPath!, AuxInstallService.DcDxgiName)));
        Assert.False(File.Exists(Path.Combine(card.InstallPath!, customDll)));
    }

    /// <summary>
    /// When switching from DC Mode Custom (level 3) to DC Mode 2,
    /// the DC file should be renamed back to winmm.dll.
    /// Validates: Requirement 4.9
    /// </summary>
    [Fact]
    public void SwitchFromDcModeCustom_ToMode2_RenamesBackToWinmm()
    {
        // Arrange
        var gameName = "Mode2Game_" + Guid.NewGuid().ToString("N")[..6];
        var customDll = "d3d11.dll";
        var vm = CreateViewModel();
        vm.SetDcCustomDllFileName(gameName, customDll);
        vm.DcModeLevel = 0;

        var card = CreateCard(gameName, perGameDcMode: 2, dcInstalled: true,
            installedAs: customDll);
        InjectCards(vm, new List<GameCardViewModel> { card });

        // Act: switch from mode 3 (custom) to mode 2
        vm.ApplyDcModeSwitchForCard(gameName, 3);

        // Assert: DC file renamed to winmm.dll
        Assert.Equal(AuxInstallService.DcWinmmName, card.DcRecord!.InstalledAs);
        Assert.True(File.Exists(Path.Combine(card.InstallPath!, AuxInstallService.DcWinmmName)));
        Assert.False(File.Exists(Path.Combine(card.InstallPath!, customDll)));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private MainViewModel CreateViewModel()
    {
        var auxInstaller = new StubAuxInstallService();
        var gameDetection = new StubGameDetectionService();
        var installer = new StubModInstallService();
        var lumaService = new StubLumaService();
        var settingsVm = new SettingsViewModel();
        var filterVm = new FilterViewModel();
        var updateOrch = new UpdateOrchestrationService(installer, auxInstaller);
        var dllOverride = new DllOverrideService(auxInstaller);
        var gameName = new GameNameService(gameDetection, installer, auxInstaller, lumaService);
        var rsUpdate = new StubReShadeUpdateService();
        var shaderPack = new StubShaderPackService();
        var gameInit = new GameInitializationService(
            gameDetection, new StubWikiService(), new StubManifestService(),
            installer, auxInstaller, new StubGameLibraryService(),
            new StubPeHeaderService(), lumaService, rsUpdate, shaderPack);

        return new MainViewModel(
            new HttpClient(),
            installer,
            auxInstaller,
            new StubWikiService(),
            new StubManifestService(),
            new StubGameLibraryService(),
            gameDetection,
            new StubPeHeaderService(),
            new StubUpdateService(),
            shaderPack,
            lumaService,
            rsUpdate,
            settingsVm,
            filterVm,
            updateOrch,
            dllOverride,
            gameName,
            gameInit);
    }

    private static void InjectCards(MainViewModel vm, List<GameCardViewModel> cards)
    {
        var field = typeof(MainViewModel).GetField("_allCards", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(vm, cards);
    }

    private GameCardViewModel CreateCard(
        string name,
        int? perGameDcMode = null,
        bool dcInstalled = true,
        string? installedAs = null)
    {
        var dir = Path.Combine(_tempRoot, name + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);

        var dcFile = installedAs ?? AuxInstallService.DcNormalName;

        var card = new GameCardViewModel
        {
            GameName = name,
            InstallPath = dir,
            PerGameDcMode = perGameDcMode,
        };

        if (dcInstalled)
        {
            File.WriteAllBytes(Path.Combine(dir, dcFile), new byte[] { 0x00 });
            card.DcStatus = GameStatus.Installed;
            card.DcRecord = new AuxInstalledRecord
            {
                GameName = name,
                InstallPath = dir,
                InstalledAs = dcFile,
                AddonType = "DisplayCommander"
            };
        }
        else
        {
            card.DcStatus = GameStatus.NotInstalled;
            card.DcRecord = null;
        }

        return card;
    }

    // ── Minimal stubs ────────────────────────────────────────────────────────

    private class StubShaderPackService : IShaderPackService
    {
        public IReadOnlyList<(string Id, string DisplayName, ShaderPackService.PackCategory Category)> AvailablePacks { get; } =
            new List<(string, string, ShaderPackService.PackCategory)>();
        public string? GetPackDescription(string packId) => null;
        public Task EnsureLatestAsync(IProgress<string>? progress = null) => Task.CompletedTask;
        public void DeployToDcFolder() { }
        public void DeployToGameFolder(string gameDir, IEnumerable<string>? packIds = null) { }
        public void RemoveFromGameFolder(string gameDir) { }
        public bool IsManagedByRdxc(string gameDir) => false;
        public void RestoreOriginalIfPresent(string gameDir) { }
        public void SyncDcFolder(IEnumerable<string>? selectedPackIds = null) { }
        public void SyncGameFolder(string gameDir, IEnumerable<string>? selectedPackIds = null) { }
        public void SyncShadersToAllLocations(
            IEnumerable<(string installPath, bool dcInstalled, bool rsInstalled, bool dcMode, string? shaderModeOverride)> locations,
            IEnumerable<string>? selectedPackIds = null) { }
    }

    private class StubModInstallService : IModInstallService
    {
        public event Action<InstalledModRecord>? InstallCompleted;
        public Task<InstalledModRecord> InstallAsync(GameMod mod, string gameInstallPath, IProgress<(string, double)>? progress = null, string? gameName = null) => Task.FromResult(new InstalledModRecord());
        public Task<bool> CheckForUpdateAsync(InstalledModRecord record) => Task.FromResult(false);
        public void Uninstall(InstalledModRecord record) { }
        public List<InstalledModRecord> LoadAll() => new();
        public InstalledModRecord? FindRecord(string gameName, string? installPath = null) => null;
        public void SaveRecordPublic(InstalledModRecord record) { }
        public void RemoveRecord(InstalledModRecord record) { }
    }

    private class StubAuxInstallService : IAuxInstallService
    {
        public Task<AuxInstalledRecord> InstallDcAsync(string gameName, string installPath, int dcModeLevel, AuxInstalledRecord? existingDcRecord = null, AuxInstalledRecord? existingRsRecord = null, string? shaderModeOverride = null, bool use32Bit = false, string? filenameOverride = null, IEnumerable<string>? selectedPackIds = null, IProgress<(string, double)>? progress = null) => Task.FromResult(new AuxInstalledRecord());
        public Task<AuxInstalledRecord> InstallReShadeAsync(string gameName, string installPath, bool dcMode, bool dcIsInstalled = false, string? shaderModeOverride = null, bool use32Bit = false, string? filenameOverride = null, IEnumerable<string>? selectedPackIds = null, IProgress<(string, double)>? progress = null) => Task.FromResult(new AuxInstalledRecord());
        public Task<bool> CheckForUpdateAsync(AuxInstalledRecord record) => Task.FromResult(false);
        public void Uninstall(AuxInstalledRecord record) { }
        public void UninstallDllOnly(AuxInstalledRecord record) { }
        public List<AuxInstalledRecord> LoadAll() => new();
        public AuxInstalledRecord? FindRecord(string gameName, string installPath, string addonType) => null;
        public void SaveAuxRecord(AuxInstalledRecord record) { }
        public void RemoveRecord(AuxInstalledRecord record) { }
    }

    private class StubWikiService : IWikiService
    {
        public Task<(List<GameMod> Mods, Dictionary<string, string> GenericNotes)> FetchAllAsync(IProgress<string>? progress = null) => Task.FromResult((new List<GameMod>(), new Dictionary<string, string>()));
        public Task<DateTime?> GetSnapshotLastModifiedAsync(string url) => Task.FromResult<DateTime?>(null);
    }

    private class StubManifestService : IManifestService
    {
        public Task<RemoteManifest?> FetchAsync() => Task.FromResult<RemoteManifest?>(null);
        public RemoteManifest? LoadCached() => null;
    }

    private class StubGameLibraryService : IGameLibraryService
    {
        public SavedGameLibrary? Load() => null;
        public void Save(List<DetectedGame> games, Dictionary<string, bool> addonCache, HashSet<string> hiddenGames, HashSet<string> favouriteGames, List<DetectedGame> manualGames, Dictionary<string, string>? engineTypeCache = null, Dictionary<string, string>? resolvedPathCache = null, Dictionary<string, string>? addonFileCache = null, Dictionary<string, MachineType>? bitnessCache = null) { }
        public List<DetectedGame> ToDetectedGames(SavedGameLibrary lib) => new();
        public List<DetectedGame> ToManualGames(SavedGameLibrary lib) => new();
    }

    private class StubGameDetectionService : IGameDetectionService
    {
        public List<DetectedGame> FindSteamGames() => new();
        public List<DetectedGame> FindGogGames() => new();
        public List<DetectedGame> FindEpicGames() => new();
        public List<DetectedGame> FindEaGames() => new();
        public List<DetectedGame> FindXboxGames() => new();
        public List<DetectedGame> FindUbisoftGames() => new();
        public List<DetectedGame> FindBattleNetGames() => new();
        public List<DetectedGame> FindRockstarGames() => new();
        public (string installPath, EngineType engine) DetectEngineAndPath(string rootPath) => (rootPath, EngineType.Unknown);
        public GameMod? MatchGame(DetectedGame game, IEnumerable<GameMod> mods, Dictionary<string, string>? nameMappings = null) => null;
        public string NormalizeName(string name) => name.ToLowerInvariant();
    }

    private class StubPeHeaderService : IPeHeaderService
    {
        public MachineType DetectArchitecture(string exePath) => MachineType.Native;
        public string? FindGameExe(string installPath) => null;
        public MachineType DetectGameArchitecture(string installPath) => MachineType.Native;
    }

    private class StubUpdateService : IUpdateService
    {
        public Version CurrentVersion => new(1, 0, 0);
        public Task<UpdateInfo?> CheckForUpdateAsync(bool betaOptIn = false) => Task.FromResult<UpdateInfo?>(null);
        public Task<string?> DownloadInstallerAsync(string downloadUrl, IProgress<(string, double)>? progress = null) => Task.FromResult<string?>(null);
        public void LaunchInstallerAndExit(string installerPath, Action closeApp) { }
    }

    private class StubLumaService : ILumaService
    {
        public Task<List<LumaMod>> FetchCompletedModsAsync(IProgress<string>? progress = null) => Task.FromResult(new List<LumaMod>());
        public Task<LumaInstalledRecord> InstallAsync(LumaMod mod, string gameInstallPath, IProgress<(string, double)>? progress = null) => Task.FromResult(new LumaInstalledRecord());
        public void Uninstall(LumaInstalledRecord record) { }
        public void SaveLumaRecord(LumaInstalledRecord record) { }
        public void RemoveLumaRecord(string gameName, string installPath) { }
    }

    private class StubReShadeUpdateService : IReShadeUpdateService
    {
        public Task<(string version, string url)?> CheckLatestVersionAsync() => Task.FromResult<(string, string)?>(null);
        public Task<bool> EnsureLatestAsync(IProgress<(string, double)>? progress = null) => Task.FromResult(false);
    }
}
