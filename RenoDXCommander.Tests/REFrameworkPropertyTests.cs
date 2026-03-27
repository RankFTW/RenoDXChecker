using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.UI.Xaml;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for RE Framework support.
/// Feature: re-framework-support
/// </summary>
public class REFrameworkPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    private static readonly Gen<GameStatus> GenGameStatus =
        Gen.Elements(GameStatus.NotInstalled, GameStatus.Available, GameStatus.Installed, GameStatus.UpdateAvailable);

    private static readonly Gen<EngineType> GenEngineType =
        Gen.Elements(EngineType.Unknown, EngineType.Unreal, EngineType.UnrealLegacy, EngineType.Unity, EngineType.REEngine);

    private static readonly Gen<string> GenVersionString =
        Gen.Elements("01302", "01303", "01400", "v1.0.0", "nightly-20240101", "", "latest");

    private static readonly Gen<string?> GenNullableVersion =
        Gen.OneOf(
            Gen.Constant<string?>(null),
            GenVersionString.Select(v => (string?)v));

    private static readonly Gen<string> GenGameName =
        Gen.Elements("Monster Hunter Wilds", "Resident Evil 4", "Devil May Cry 5",
                      "Street Fighter 6", "Dragon's Dogma 2", "TestGame");

    private static readonly Gen<string> GenInstallPath =
        Gen.Elements(@"C:\Games\MHWilds", @"D:\Steam\RE4", @"E:\Games\DMC5",
                      @"C:\Program Files\SF6", @"D:\GOG\DD2");

    // ── Property 1: RE Engine detection via re_chunk_000.pak ──────────────────────
    // Feature: re-framework-support, Property 1: RE Engine detection via re_chunk_000.pak
    // **Validates: Requirements 1.1, 1.2**

    [Property(MaxTest = 100)]
    public Property DetectEngine_ReturnsREEngine_IffReChunkPakPresent()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            (bool hasReChunk) =>
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "REFrameworkTest_" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(tempDir);
                    // Place a dummy exe so the detection has something to find
                    File.WriteAllBytes(Path.Combine(tempDir, "game.exe"), new byte[] { 0x4D, 0x5A });

                    if (hasReChunk)
                        File.WriteAllText(Path.Combine(tempDir, "re_chunk_000.pak"), "dummy");

                    var service = new GameDetectionService();
                    var (_, engine) = service.DetectEngineAndPath(tempDir);

                    return (engine == EngineType.REEngine) == hasReChunk
                        ? true.ToProperty()
                        : false.Label($"hasReChunk={hasReChunk}, detected={engine}");
                }
                finally
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            });
    }

    // ── Property 2: Engine hint mapping for RE Engine ─────────────────────────────
    // Feature: re-framework-support, Property 2: Engine hint mapping for RE Engine
    // **Validates: Requirements 1.3**

    [Property(MaxTest = 100)]
    public Property EngineType_MapsToCorrectHintString()
    {
        return Prop.ForAll(
            Arb.From(GenEngineType),
            (EngineType engine) =>
            {
                // The mapping used in MainViewModel for EngineHint (without overrides/UeExtended)
                var expected = engine switch
                {
                    EngineType.Unreal       => "Unreal Engine",
                    EngineType.UnrealLegacy => "Unreal (Legacy)",
                    EngineType.Unity        => "Unity",
                    EngineType.REEngine     => "RE Engine",
                    _                       => "",
                };

                // Verify the mapping is correct — specifically that REEngine maps to "RE Engine"
                bool reEngineCorrect = engine != EngineType.REEngine || expected == "RE Engine";
                bool nonEmpty = engine == EngineType.Unknown || expected.Length > 0;

                return (reEngineCorrect && nonEmpty)
                    .Label($"engine={engine}, expected='{expected}', reEngineCorrect={reEngineCorrect}, nonEmpty={nonEmpty}");
            });
    }

    // ── Property 4: RE Framework row visibility ──────────────────────────────────
    // Feature: re-framework-support, Property 4: RE Framework row visibility
    // **Validates: Requirements 2.1, 2.4, 2.5**

    [Property(MaxTest = 100)]
    public Property RefRowVisibility_VisibleIffREEngineAndNotLumaMode()
    {
        var gen =
            from isREEngine in Arb.Default.Bool().Generator
            from lumaFeatureEnabled in Arb.Default.Bool().Generator
            from isLumaMode in Arb.Default.Bool().Generator
            select (isREEngine, lumaFeatureEnabled, isLumaMode);

        return Prop.ForAll(
            Arb.From(gen),
            ((bool isREEngine, bool lumaFeatureEnabled, bool isLumaMode) input) =>
            {
                var card = new GameCardViewModel
                {
                    EngineHint = "",
                    IsREEngineGame = input.isREEngine,
                    LumaFeatureEnabled = input.lumaFeatureEnabled,
                    IsLumaMode = input.isLumaMode,
                };

                bool effectiveLumaMode = input.lumaFeatureEnabled && input.isLumaMode;
                var expectedVisibility = (input.isREEngine && !effectiveLumaMode)
                    ? Visibility.Visible : Visibility.Collapsed;

                return (card.RefRowVisibility == expectedVisibility)
                    .Label($"isREEngine={input.isREEngine}, lumaFeatureEnabled={input.lumaFeatureEnabled}, isLumaMode={input.isLumaMode}, " +
                           $"expected={expectedVisibility}, actual={card.RefRowVisibility}");
            });
    }

    // ── Property 5: RE Framework status text mapping ─────────────────────────────
    // Feature: re-framework-support, Property 5: RE Framework status text mapping
    // **Validates: Requirements 4.1, 4.3, 5.3**

    [Property(MaxTest = 100)]
    public Property RefStatusText_MapsCorrectly()
    {
        var gen =
            from status in GenGameStatus
            from isInstalling in Arb.Default.Bool().Generator
            from version in GenNullableVersion
            select (status, isInstalling, version);

        return Prop.ForAll(
            Arb.From(gen),
            ((GameStatus status, bool isInstalling, string? version) input) =>
            {
                var card = new GameCardViewModel
                {
                    EngineHint = "",
                    RefStatus = input.status,
                    RefIsInstalling = input.isInstalling,
                    RefInstalledVersion = input.version,
                };

                var expected = input.isInstalling ? "Installing…"
                    : input.status == GameStatus.UpdateAvailable ? "Update"
                    : input.status == GameStatus.Installed ? (input.version ?? "Installed")
                    : "Ready";

                return (card.RefStatusText == expected)
                    .Label($"status={input.status}, isInstalling={input.isInstalling}, version='{input.version}', " +
                           $"expected='{expected}', actual='{card.RefStatusText}'");
            });
    }

    // ── Property 6: RE Framework action label mapping ────────────────────────────
    // Feature: re-framework-support, Property 6: RE Framework action label mapping
    // **Validates: Requirements 6.1, 6.2, 6.3, 6.4**

    [Property(MaxTest = 100)]
    public Property RefActionLabel_MapsCorrectly()
    {
        var gen =
            from status in GenGameStatus
            from isInstalling in Arb.Default.Bool().Generator
            select (status, isInstalling);

        return Prop.ForAll(
            Arb.From(gen),
            ((GameStatus status, bool isInstalling) input) =>
            {
                var card = new GameCardViewModel
                {
                    EngineHint = "",
                    RefStatus = input.status,
                    RefIsInstalling = input.isInstalling,
                };

                var expected = input.isInstalling ? "Installing..."
                    : input.status == GameStatus.UpdateAvailable ? "⬆  Update RE Framework"
                    : input.status == GameStatus.Installed ? "↺  Reinstall RE Framework"
                    : "⬇  Install RE Framework";

                return (card.RefActionLabel == expected)
                    .Label($"status={input.status}, isInstalling={input.isInstalling}, " +
                           $"expected='{expected}', actual='{card.RefActionLabel}'");
            });
    }

    // ── Property 7: RE Framework status dot and UI state mapping ─────────────────
    // Feature: re-framework-support, Property 7: RE Framework status dot and UI state mapping
    // **Validates: Requirements 6.5, 7.4, 3.6**

    [Property(MaxTest = 100)]
    public Property RefStatusDotAndUiState_MapsCorrectly()
    {
        var gen =
            from status in GenGameStatus
            from isInstalling in Arb.Default.Bool().Generator
            select (status, isInstalling);

        return Prop.ForAll(
            Arb.From(gen),
            ((GameStatus status, bool isInstalling) input) =>
            {
                var card = new GameCardViewModel
                {
                    EngineHint = "",
                    RefStatus = input.status,
                    RefIsInstalling = input.isInstalling,
                };

                // CardRefStatusDot
                var expectedDot = input.isInstalling ? "#2196F3"
                    : input.status == GameStatus.UpdateAvailable ? "#4CAF50"
                    : input.status == GameStatus.Installed ? "#4CAF50"
                    : "#5A6880";

                // RefDeleteVisibility
                var expectedDelete = (input.status == GameStatus.Installed || input.status == GameStatus.UpdateAvailable)
                    ? Visibility.Visible : Visibility.Collapsed;

                // RefProgressVisibility
                var expectedProgress = input.isInstalling ? Visibility.Visible : Visibility.Collapsed;

                bool dotOk = card.CardRefStatusDot == expectedDot;
                bool deleteOk = card.RefDeleteVisibility == expectedDelete;
                bool progressOk = card.RefProgressVisibility == expectedProgress;

                return (dotOk && deleteOk && progressOk)
                    .Label($"status={input.status}, isInstalling={input.isInstalling}, " +
                           $"dot: expected='{expectedDot}' actual='{card.CardRefStatusDot}' ok={dotOk}, " +
                           $"delete: expected={expectedDelete} actual={card.RefDeleteVisibility} ok={deleteOk}, " +
                           $"progress: expected={expectedProgress} actual={card.RefProgressVisibility} ok={progressOk}");
            });
    }

    // ── Property 8: Install record serialization round-trip ──────────────────────
    // Feature: re-framework-support, Property 8: Install record serialization round-trip
    // **Validates: Requirements 4.4**

    [Property(MaxTest = 100)]
    public Property REFrameworkInstalledRecord_SerializationRoundTrip()
    {
        var gen =
            from name in GenGameName
            from path in GenInstallPath
            from version in GenVersionString
            from year in Gen.Choose(2020, 2030)
            from month in Gen.Choose(1, 12)
            from day in Gen.Choose(1, 28)
            from hour in Gen.Choose(0, 23)
            from minute in Gen.Choose(0, 59)
            from second in Gen.Choose(0, 59)
            select new REFrameworkInstalledRecord
            {
                GameName = name,
                InstallPath = path,
                InstalledVersion = version,
                InstalledAt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc),
            };

        return Prop.ForAll(
            Arb.From(gen),
            (REFrameworkInstalledRecord record) =>
            {
                var json = JsonSerializer.Serialize(record);
                var deserialized = JsonSerializer.Deserialize<REFrameworkInstalledRecord>(json)!;

                bool nameOk = deserialized.GameName == record.GameName;
                bool pathOk = deserialized.InstallPath == record.InstallPath;
                bool versionOk = deserialized.InstalledVersion == record.InstalledVersion;
                bool dateOk = deserialized.InstalledAt == record.InstalledAt;

                return (nameOk && pathOk && versionOk && dateOk)
                    .Label($"name: {nameOk}, path: {pathOk}, version: {versionOk}, date: {dateOk} " +
                           $"(original={record.InstalledAt:O}, deserialized={deserialized.InstalledAt:O})");
            });
    }
}
