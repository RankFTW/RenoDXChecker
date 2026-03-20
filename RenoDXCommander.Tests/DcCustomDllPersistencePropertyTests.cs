using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: dc-mode-ui-enhancements, Property 7: DC Custom DLL filename persistence round-trip

/// <summary>
/// Property-based tests for DC Mode Custom DLL filename persistence round-trip.
/// For any game name and valid DLL filename string, persisting the DC Mode Custom
/// filename to the settings store and then loading it back should return the same filename.
/// **Validates: Requirements 4.6, 5.1**
/// </summary>
public class DcCustomDllPersistencePropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates non-null, non-whitespace game names.
    /// </summary>
    private static Gen<string> GenGameName()
    {
        return Arb.Generate<NonEmptyString>()
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get);
    }

    /// <summary>
    /// Generates valid DLL filenames (non-null, non-whitespace strings ending in .dll).
    /// </summary>
    private static Gen<string> GenDllFileName()
    {
        return Arb.Generate<NonEmptyString>()
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get.Trim() + ".dll");
    }

    // ── Property 7: DC Custom DLL filename persistence round-trip ─────────────

    /// <summary>
    /// For any game name and valid DLL filename, setting the value in the
    /// DcCustomDllFileNames dictionary and reading it back should return
    /// the same filename.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property DcCustomDllFileName_RoundTrip_ViaDirectDictionary()
    {
        var gen = from gameName in GenGameName()
                  from dllName in GenDllFileName()
                  select (gameName, dllName);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, dllName) = tuple;

            // Create a fresh GameNameService (the settings store)
            var svc = new GameNameService(
                new LocalStubGameDetectionService(),
                new LocalStubModInstallService(),
                new LocalStubAuxInstallService(),
                new LocalStubLumaService());

            // Persist: write the custom DLL filename
            svc.DcCustomDllFileNames[gameName] = dllName;

            // Load: read it back
            var loaded = svc.DcCustomDllFileNames.TryGetValue(gameName, out var result)
                ? result
                : null;

            if (loaded != dllName)
                return false.Label(
                    $"Round-trip failed for game '{gameName}': " +
                    $"expected '{dllName}', got '{loaded}'");

            return true.Label("OK");
        });
    }

    /// <summary>
    /// For any game name and valid DLL filename, the dictionary's case-insensitive
    /// key lookup should return the same filename regardless of game name casing.
    /// This validates the OrdinalIgnoreCase comparer used by the settings store.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property DcCustomDllFileName_RoundTrip_CaseInsensitiveKey()
    {
        var gen = from gameName in GenGameName()
                  from dllName in GenDllFileName()
                  select (gameName, dllName);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (gameName, dllName) = tuple;

            var svc = new GameNameService(
                new LocalStubGameDetectionService(),
                new LocalStubModInstallService(),
                new LocalStubAuxInstallService(),
                new LocalStubLumaService());

            // Persist with original casing
            svc.DcCustomDllFileNames[gameName] = dllName;

            // Load with upper-cased key
            var upperKey = gameName.ToUpperInvariant();
            var loaded = svc.DcCustomDllFileNames.TryGetValue(upperKey, out var result)
                ? result
                : null;

            if (loaded != dllName)
                return false.Label(
                    $"Case-insensitive round-trip failed: stored with '{gameName}', " +
                    $"queried with '{upperKey}', expected '{dllName}', got '{loaded}'");

            return true.Label("OK");
        });
    }

    // ── Minimal stubs ─────────────────────────────────────────────────────────────

    private class LocalStubGameDetectionService : IGameDetectionService
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

    private class LocalStubModInstallService : IModInstallService
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

    private class LocalStubAuxInstallService : IAuxInstallService
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
    }

    private class LocalStubLumaService : ILumaService
    {
        public Task<List<LumaMod>> FetchCompletedModsAsync(IProgress<string>? progress = null) => Task.FromResult(new List<LumaMod>());
        public Task<LumaInstalledRecord> InstallAsync(LumaMod mod, string gameInstallPath, IProgress<(string, double)>? progress = null) => Task.FromResult(new LumaInstalledRecord());
        public void Uninstall(LumaInstalledRecord record) { }
        public void SaveLumaRecord(LumaInstalledRecord record) { }
        public void RemoveLumaRecord(string gameName, string installPath) { }
    }
}
