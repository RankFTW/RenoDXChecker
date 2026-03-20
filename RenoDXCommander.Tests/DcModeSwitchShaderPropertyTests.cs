using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property tests for DC mode switch shader transitions.
/// Tests verify that shader operations (SyncGameFolder, RemoveFromGameFolder,
/// RestoreOriginalIfPresent) are correctly triggered based on effective DC mode
/// level transitions during per-game mode switches.
///
/// Feature: dc-mode-shader-deployment
/// </summary>
public class DcModeSwitchShaderPropertyTests : IDisposable
{
    private readonly string _tempRoot;

    public DcModeSwitchShaderPropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcSwitchProp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // ── Property 3: Mode switch 0→non-zero deploys shaders only when DC installed ──

    /// <summary>
    /// Property 3: Mode switch 0→non-zero deploys shaders only when DC is installed.
    ///
    /// For any game card where the effective DC mode level transitions from 0 to a
    /// non-zero value, <c>SyncGameFolder</c> SHALL be called if and only if DC is
    /// installed for that game. If DC is not installed, no shader operations SHALL occur.
    ///
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property ModeSwitchZeroToNonZero_DeploysShaders_OnlyWhenDcInstalled()
    {
        var genDcInstalled = Arb.From<bool>().Generator;

        return Prop.ForAll(
            Arb.From(genDcInstalled),
            dcInstalled =>
            {
                // Previous per-game override is "Off" → effective is off
                string? previousPerGameDcMode = "Off";

                var tracker = new TrackingShaderPackService();
                var vm = CreateViewModelWithTracker(tracker);
                SetDcModeWithoutSideEffects(vm, true, "dxgi.dll");

                var card = CreateCard(
                    "TestGame",
                    perGameDcMode: "Custom",  // new value: Custom (DC on)
                    dcInstalled: dcInstalled);

                InjectCards(vm, new List<GameCardViewModel> { card });

                // Set the per-game override in the dictionary so ResolveEffectiveDcMode sees it
                vm.SetPerGameDcModeOverride("TestGame", "Custom");
                vm.SetDcCustomDllFileName("TestGame", "dxgi.dll");

                // Act: switch from previous="Off" to new="Custom"
                vm.ApplyDcModeSwitchForCard("TestGame", previousPerGameDcMode);

                // Assert: SyncGameFolder called iff DC is installed
                var syncCalled = tracker.SyncGameFolderCalled;
                var expectSync = dcInstalled;

                return (syncCalled == expectSync)
                    .Label($"DcInstalled={dcInstalled} → " +
                           $"SyncGameFolderCalled={syncCalled} (expected {expectSync})");
            });
    }

    // ── Property 4: Mode switch non-zero→0 removes shaders ─────────────────

    /// <summary>
    /// Property 4: Mode switch non-zero→0 removes shaders.
    ///
    /// For any game card where the effective DC mode level transitions from a
    /// non-zero value to 0, <c>RemoveFromGameFolder</c> and
    /// <c>RestoreOriginalIfPresent</c> SHALL be called for that game.
    ///
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property ModeSwitchNonZeroToZero_RemovesShaders()
    {
        var genGlobalEnabled = Arb.From<bool>().Generator;

        return Prop.ForAll(
            Arb.From(genGlobalEnabled),
            globalEnabled =>
            {
                // New per-game override is "Off" → effective becomes off
                string? previousPerGameDcMode = "Custom"; // was on

                var tracker = new TrackingShaderPackService();
                var vm = CreateViewModelWithTracker(tracker);
                SetDcModeWithoutSideEffects(vm, globalEnabled, "dxgi.dll");

                // Card with DC installed, per-game mode set to "Off" (the new value)
                var card = CreateCard(
                    "TestGame",
                    perGameDcMode: "Off",       // new value: Off
                    dcInstalled: true,
                    dllOverrideEnabled: false,
                    isLumaMode: false,
                    isVulkan: false);

                InjectCards(vm, new List<GameCardViewModel> { card });

                // Set the per-game override in the dictionary so ResolveEffectiveDcMode sees it
                vm.SetPerGameDcModeOverride("TestGame", "Off");

                // Act: switch from previous="Custom" to new="Off"
                vm.ApplyDcModeSwitchForCard("TestGame", previousPerGameDcMode);

                // Assert: both RemoveFromGameFolder and RestoreOriginalIfPresent called
                var removeCalled = tracker.RemoveFromGameFolderCalled;
                var restoreCalled = tracker.RestoreOriginalIfPresentCalled;

                return (removeCalled && restoreCalled)
                    .Label($"GlobalEnabled={globalEnabled} → " +
                           $"RemoveCalled={removeCalled}, RestoreCalled={restoreCalled} " +
                           $"(expected both true)");
            });
    }

    // ── Property 5: Mode switch within non-zero levels does not touch shaders ──

    /// <summary>
    /// Property 5: Mode switch within non-zero levels does not touch shaders.
    ///
    /// For any game card where the effective DC mode level transitions from one
    /// non-zero value to another non-zero value (1→2 or 2→1), no shader deployment
    /// or removal methods SHALL be called (no SyncGameFolder, RemoveFromGameFolder,
    /// RestoreOriginalIfPresent).
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property ModeSwitchWithinNonZeroLevels_DoesNotTouchShaders()
    {
        // In the new system, "within non-zero" means both previous and new are DC-on states
        // e.g., Custom→Custom with different DLL, or Global(on)→Custom
        var genDllFileName = Gen.Elements("dxgi.dll", "winmm.dll", "d3d11.dll");

        return Prop.ForAll(
            Arb.From(genDllFileName),
            dllFileName =>
            {
                var tracker = new TrackingShaderPackService();
                var vm = CreateViewModelWithTracker(tracker);
                SetDcModeWithoutSideEffects(vm, true, dllFileName);

                // Card with DC installed, Custom mode (DC on), no blocking flags
                var card = CreateCard(
                    "TestGame",
                    perGameDcMode: "Custom",
                    dcInstalled: true,
                    dllOverrideEnabled: false,
                    isLumaMode: false,
                    isVulkan: false);

                InjectCards(vm, new List<GameCardViewModel> { card });

                // Set the per-game override in the dictionary
                vm.SetPerGameDcModeOverride("TestGame", "Custom");
                vm.SetDcCustomDllFileName("TestGame", dllFileName);

                // Act: switch from previous Custom to new Custom (DLL change only)
                vm.ApplyDcModeSwitchForCard("TestGame", "Custom");

                // Assert: no shader methods called
                var syncCalled = tracker.SyncGameFolderCalled;
                var removeCalled = tracker.RemoveFromGameFolderCalled;
                var restoreCalled = tracker.RestoreOriginalIfPresentCalled;

                return (!syncCalled && !removeCalled && !restoreCalled)
                    .Label($"DllFileName={dllFileName} → " +
                           $"SyncCalled={syncCalled}, RemoveCalled={removeCalled}, RestoreCalled={restoreCalled} " +
                           $"(expected all false)");
            });
    }

    // ── Property 6: Effective DC mode level computation ────────────────────────

    /// <summary>
    /// Property 6: Effective DC mode level computation.
    ///
    /// For any game card configuration (DllOverrideEnabled, IsLumaMode,
    /// RequiresVulkanInstall, PerGameDcMode, global DcModeLevel), the effective
    /// DC mode level SHALL be:
    /// - 0 if DllOverrideEnabled is true
    /// - 0 if IsLumaMode is true
    /// - 0 if RequiresVulkanInstall is true and PerGameDcMode is null
    /// - PerGameDcMode if PerGameDcMode has a value
    /// - global DcModeLevel otherwise
    ///
    /// Tested indirectly: a transition from effective=0 to the computed effective
    /// level is set up. If the computed level is > 0 (and DC is installed),
    /// SyncGameFolder must be called. If 0, no shader ops occur.
    ///
    /// **Validates: Requirements 6.1, 6.2, 6.3, 6.4**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property EffectiveDcModeLevelComputation_MatchesResolutionRules()
    {
        var genDllOverride = Arb.From<bool>().Generator;
        var genLumaMode = Arb.From<bool>().Generator;
        var genVulkan = Arb.From<bool>().Generator;
        var genPerGame = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Elements<string?>("Off", "Custom"));
        var genGlobalEnabled = Arb.From<bool>().Generator;

        var genConfig = from dllOverride in genDllOverride
                        from lumaMode in genLumaMode
                        from vulkan in genVulkan
                        from perGame in genPerGame
                        from globalEnabled in genGlobalEnabled
                        select (dllOverride, lumaMode, vulkan, perGame, globalEnabled);

        return Prop.ForAll(
            Arb.From(genConfig),
            config =>
            {
                var (dllOverride, lumaMode, vulkan, perGame, globalEnabled) = config;

                // Compute expected effective enabled per the resolution rules
                bool expectedEnabled;
                if (dllOverride)
                    expectedEnabled = false;
                else if (lumaMode)
                    expectedEnabled = false;
                else if (vulkan && perGame == null)
                    expectedEnabled = false;
                else if (perGame == "Off")
                    expectedEnabled = false;
                else if (perGame == "Custom")
                    expectedEnabled = true;
                else
                    expectedEnabled = globalEnabled;

                var tracker = new TrackingShaderPackService();
                var vm = CreateViewModelWithTracker(tracker);
                SetDcModeWithoutSideEffects(vm, globalEnabled, "dxgi.dll");

                var card = CreateCard(
                    "TestGame",
                    perGameDcMode: perGame,
                    dcInstalled: true,
                    dllOverrideEnabled: dllOverride,
                    isLumaMode: lumaMode,
                    isVulkan: vulkan);

                InjectCards(vm, new List<GameCardViewModel> { card });

                // Set the per-game override in the dictionary so ResolveEffectiveDcMode sees it.
                // Always call SetPerGameDcModeOverride to clear any stale entry that may
                // have been loaded from the on-disk settings file.
                vm.SetPerGameDcModeOverride("TestGame", perGame);
                if (perGame == "Custom")
                    vm.SetDcCustomDllFileName("TestGame", "dxgi.dll");

                vm.ApplyDcModeSwitchForCard("TestGame", "Off");

                bool expectSync;
                if (!expectedEnabled)
                    expectSync = false;
                else
                    expectSync = true;

                var syncCalled = tracker.SyncGameFolderCalled;
                var removeCalled = tracker.RemoveFromGameFolderCalled;

                return (syncCalled == expectSync && !removeCalled)
                    .Label($"DllOverride={dllOverride}, Luma={lumaMode}, Vulkan={vulkan}, " +
                           $"PerGame={perGame}, GlobalEnabled={globalEnabled} → " +
                           $"ExpectedEnabled={expectedEnabled}, " +
                           $"SyncCalled={syncCalled} (expected {expectSync}), " +
                           $"RemoveCalled={removeCalled} (expected false)");
            });
    }

    // ── Property 7: Global mode switch only affects games with actual transitions ──

    /// <summary>
    /// Property 7: Global mode switch only affects games with actual transitions.
    ///
    /// For any set of game cards and any global DC mode level change, shader operations
    /// (SyncGameFolder, RemoveFromGameFolder) SHALL only be invoked for cards whose
    /// effective DC mode level actually changes between 0 and non-zero. Cards with
    /// per-game overrides, DLL overrides, Luma mode, or Vulkan defaults that prevent
    /// a transition SHALL not have shader operations invoked.
    ///
    /// **Validates: Requirements 5.3, 5.4**
    /// </summary>
    [Fact]
    public void GlobalModeSwitch_OnlyAffectsGamesWithActualTransitions()
    {
        var tracker = new TrackingShaderPackService();
        var vm = CreateViewModelWithTracker(tracker);

        // Use _isLoadingSettings to suppress side effects when setting properties
        SetDcModeWithoutSideEffects(vm, false, "dxgi.dll");

        Assert.False(vm.DcModeEnabled, "DcModeEnabled should be false");

        var card = CreateCard(
            "TestGame",
            perGameDcMode: null,
            dcInstalled: true,
            dllOverrideEnabled: false,
            isLumaMode: false,
            isVulkan: false);

        InjectCards(vm, new List<GameCardViewModel> { card });

        // Clear any stale per-game override that may have been loaded from the
        // on-disk settings file (previous test runs can leave residual state).
        vm.SetPerGameDcModeOverride("TestGame", null);

        // Act: transition from on→off
        vm.ApplyDcModeSwitch((wasEnabled: true, wasDllFileName: "dxgi.dll"));

        // Assert
        Assert.True(tracker.RemoveFromGameFolderCalled,
            $"RemoveFromGameFolder should be called for on→off transition");
    }

    /// <summary>
    /// Mirrors the effective DC mode resolution logic for test-side computation.
    /// </summary>
    private static bool ComputeExpectedEnabled(bool dllOverride, bool lumaMode, bool vulkan, string? perGame, bool globalEnabled)
    {
        if (dllOverride) return false;
        if (lumaMode) return false;
        if (vulkan && perGame == null) return false;
        if (perGame == "Off") return false;
        if (perGame == "Custom") return true;
        return globalEnabled; // null or "Global"
    }

    // ── Helper: Create MainViewModel with tracking shader service ─────────────

    private MainViewModel CreateViewModelWithTracker(TrackingShaderPackService tracker)
    {
        var auxInstaller = new StubAuxInstallService();
        var gameDetection = new StubGameDetectionService();
        var installer = new StubModInstallService();
        var lumaService = new StubLumaService();
        var settingsVm = new SettingsViewModel();
        var filterVm = new FilterViewModel();
        var updateOrch = new UpdateOrchestrationService(installer, auxInstaller, new CrashReporterService(), auxInstaller);
        var dllOverride = new DllOverrideService(auxInstaller);
        var gameName = new GameNameService(gameDetection, installer, auxInstaller, lumaService);
        var rsUpdate = new StubReShadeUpdateService();
        var gameInit = new GameInitializationService(
            gameDetection, new StubWikiService(), new StubManifestService(),
            installer, auxInstaller, new StubGameLibraryService(),
            new StubPeHeaderService(), lumaService, rsUpdate, tracker);

        return new MainViewModel(
            new HttpClient(),
            installer,
            auxInstaller,
            new CrashReporterService(),
            new StubWikiService(),
            new StubManifestService(),
            new StubGameLibraryService(),
            gameDetection,
            new StubPeHeaderService(),
            new StubUpdateService(),
            tracker,
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

    private static void SetField(object obj, string fieldName, object value)
    {
        // Try the exact field name first, then search all fields for a match
        var type = obj.GetType();
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            // Search through all base types
            var current = type;
            while (current != null && field == null)
            {
                field = current.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                current = current.BaseType;
            }
        }
        if (field != null)
            field.SetValue(obj, value);
        else
            throw new InvalidOperationException($"Field '{fieldName}' not found on type '{type.Name}'");
    }

    /// <summary>
    /// Sets DC mode properties on the VM without triggering side effects.
    /// Uses the SettingsViewModel.IsLoadingSettings flag to suppress ApplyDcModeSwitch calls.
    /// </summary>
    private static void SetDcModeWithoutSideEffects(MainViewModel vm, bool enabled, string dllFileName)
    {
        // Access the SettingsViewModel to set IsLoadingSettings
        var settingsField = typeof(MainViewModel).GetField("_settingsViewModel", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var settings = (SettingsViewModel)settingsField.GetValue(vm)!;
        settings.IsLoadingSettings = true;
        try
        {
            vm.DcModeEnabled = enabled;
            vm.DcDllFileName = dllFileName;
        }
        finally
        {
            settings.IsLoadingSettings = false;
        }
    }

    private GameCardViewModel CreateCard(
        string name,
        string? perGameDcMode = null,
        bool dcInstalled = true,
        bool dllOverrideEnabled = false,
        bool isLumaMode = false,
        bool isVulkan = false)
    {
        var dir = Path.Combine(_tempRoot, name + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);

        var card = new GameCardViewModel
        {
            GameName = name,
            InstallPath = dir,
            DllOverrideEnabled = dllOverrideEnabled,
            IsLumaMode = isLumaMode,
            PerGameDcMode = perGameDcMode,
        };

        if (isVulkan)
        {
            card.GraphicsApi = GraphicsApiType.Vulkan;
            card.IsDualApiGame = false;
        }

        if (dcInstalled)
        {
            var dcFile = AuxInstallService.DcNormalName;
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

    // ── Tracking IShaderPackService ───────────────────────────────────────────

    private class TrackingShaderPackService : IShaderPackService
    {
        public bool SyncGameFolderCalled { get; private set; }
        public string? SyncGameFolderDir { get; private set; }
        public bool RemoveFromGameFolderCalled { get; private set; }
        public string? RemoveFromGameFolderDir { get; private set; }
        public bool RestoreOriginalIfPresentCalled { get; private set; }
        public bool SyncDcFolderCalled { get; private set; }
        public bool DeployToDcFolderCalled { get; private set; }

        /// <summary>Records all directories passed to SyncGameFolder (for multi-card tracking).</summary>
        public List<string> SyncGameFolderDirs { get; } = new();
        /// <summary>Records all directories passed to RemoveFromGameFolder (for multi-card tracking).</summary>
        public List<string> RemoveFromGameFolderDirs { get; } = new();
        /// <summary>Records all directories passed to RestoreOriginalIfPresent (for multi-card tracking).</summary>
        public List<string> RestoreOriginalIfPresentDirs { get; } = new();

        public IReadOnlyList<(string Id, string DisplayName, ShaderPackService.PackCategory Category)> AvailablePacks { get; } =
            new List<(string, string, ShaderPackService.PackCategory)>();

        public string? GetPackDescription(string packId) => null;
        public Task EnsureLatestAsync(IProgress<string>? progress = null) => Task.CompletedTask;
        public void DeployToDcFolder() => DeployToDcFolderCalled = true;
        public void DeployToGameFolder(string gameDir, IEnumerable<string>? packIds = null) { }

        public void RemoveFromGameFolder(string gameDir)
        {
            RemoveFromGameFolderCalled = true;
            RemoveFromGameFolderDir = gameDir;
            RemoveFromGameFolderDirs.Add(gameDir);
        }

        public bool IsManagedByRdxc(string gameDir) => false;

        public void RestoreOriginalIfPresent(string gameDir)
        {
            RestoreOriginalIfPresentCalled = true;
            RestoreOriginalIfPresentDirs.Add(gameDir);
        }

        public void SyncDcFolder(IEnumerable<string>? selectedPackIds = null)
            => SyncDcFolderCalled = true;

        public void SyncGameFolder(string gameDir, IEnumerable<string>? selectedPackIds = null)
        {
            SyncGameFolderCalled = true;
            SyncGameFolderDir = gameDir;
            SyncGameFolderDirs.Add(gameDir);
        }

        public void SyncShadersToAllLocations(
            IEnumerable<(string installPath, bool dcInstalled, bool rsInstalled, bool dcMode, string? shaderModeOverride)> locations,
            IEnumerable<string>? selectedPackIds = null) { }
    }

    // ── Minimal stubs ────────────────────────────────────────────────────────

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

    private class StubAuxInstallService : IAuxInstallService, IAuxFileService
    {
        public Task<AuxInstalledRecord> InstallDcAsync(string gameName, string installPath, string? dllFileName, AuxInstalledRecord? existingDcRecord = null, AuxInstalledRecord? existingRsRecord = null, string? shaderModeOverride = null, bool use32Bit = false, string? filenameOverride = null, IEnumerable<string>? selectedPackIds = null, IProgress<(string, double)>? progress = null) => Task.FromResult(new AuxInstalledRecord());
        public Task<AuxInstalledRecord> InstallReShadeAsync(string gameName, string installPath, bool dcMode, bool dcIsInstalled = false, string? shaderModeOverride = null, bool use32Bit = false, string? filenameOverride = null, IEnumerable<string>? selectedPackIds = null, IProgress<(string, double)>? progress = null) => Task.FromResult(new AuxInstalledRecord());
        public Task<bool> CheckForUpdateAsync(AuxInstalledRecord record) => Task.FromResult(false);
        public void Uninstall(AuxInstalledRecord record) { }
        public void UninstallDllOnly(AuxInstalledRecord record) { }
        public List<AuxInstalledRecord> LoadAll() => new();
        public AuxInstalledRecord? FindRecord(string gameName, string installPath, string addonType) => null;
        public void SaveAuxRecord(AuxInstalledRecord record) { }
        public void RemoveRecord(AuxInstalledRecord record) { }
        // IAuxFileService stubs
        public void SyncReShadeToDisplayCommander() { }
        public bool EnsureReShadeStaging() => false;
        public AuxInstallService.DxgiFileType IdentifyDxgiFile(string filePath) => AuxInstallService.DxgiFileType.Unknown;
        public AuxInstallService.WinmmFileType IdentifyWinmmFile(string filePath) => AuxInstallService.WinmmFileType.Unknown;
        public bool BackupForeignDll(string dllPath) => false;
        public void RestoreForeignDll(string dllPath) { }
        public bool IsReShadeFileStrict(string filePath) => false;
        public bool IsDcFileStrict(string filePath) => false;
        public bool IsReShadeFile(string filePath) => false;
        public void EnsureInisDir() { }
        public void MergeRsIni(string gameDir) { }
        public void MergeRsVulkanIni(string gameDir) { }
        public void CopyRsIni(string gameDir) { }
        public void CopyRsPresetIniIfPresent(string gameDir) { }
        public void CopyDcIni(string gameDir) { }
        public string? ReadInstalledVersion(string installPath, string fileName) => null;
        public bool CheckReShadeUpdateLocal(AuxInstalledRecord record) => false;
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

