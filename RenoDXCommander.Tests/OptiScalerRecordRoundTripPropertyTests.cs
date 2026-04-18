using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for AuxInstalledRecord JSON round-trip with AddonType = "OptiScaler".
/// Feature: optiscaler-integration, Property 10: Tracking Record Serialization Round-Trip
/// </summary>
public class OptiScalerRecordRoundTripPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenGameName =
        Gen.Elements(
            "Cyberpunk 2077", "Elden Ring", "Starfield",
            "Baldur's Gate 3", "Alan Wake 2", "The Witcher 3");

    private static readonly Gen<string> GenInstallPath =
        Gen.Elements(
            @"C:\Games\Cyberpunk 2077\bin\x64",
            @"D:\SteamLibrary\steamapps\common\Elden Ring\Game",
            @"C:\Program Files\Steam\steamapps\common\Starfield",
            @"E:\Games\BG3\bin");

    private static readonly Gen<string> GenInstalledAs =
        Gen.Elements(OptiScalerService.SupportedDllNames);

    private static readonly Gen<DateTime> GenInstalledAt =
        Gen.Choose(2020, 2030).SelectMany(year =>
        Gen.Choose(1, 12).SelectMany(month =>
        Gen.Choose(1, 28).SelectMany(day =>
        Gen.Choose(0, 23).SelectMany(hour =>
        Gen.Choose(0, 59).SelectMany(minute =>
        Gen.Choose(0, 59).Select(second =>
            new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc)))))));

    /// <summary>
    /// Generates a complete AuxInstalledRecord with AddonType = "OptiScaler".
    /// </summary>
    private static readonly Gen<AuxInstalledRecord> GenOptiScalerRecord =
        GenGameName.SelectMany(gameName =>
        GenInstallPath.SelectMany(installPath =>
        GenInstalledAs.SelectMany(installedAs =>
        GenInstalledAt.Select(installedAt =>
            new AuxInstalledRecord
            {
                GameName = gameName,
                InstallPath = installPath,
                AddonType = OptiScalerService.AddonType,
                InstalledAs = installedAs,
                SourceUrl = null,
                RemoteFileSize = null,
                InstalledAt = installedAt,
            }))));

    // ── Property 10: Tracking Record Serialization Round-Trip ─────────────────────
    // Feature: optiscaler-integration, Property 10: Tracking Record Serialization Round-Trip
    // **Validates: Requirements 6.1, 6.5**

    /// <summary>
    /// For any valid AuxInstalledRecord with AddonType = "OptiScaler", arbitrary GameName,
    /// InstallPath, InstalledAs from supported DLL names, and valid InstalledAt timestamp,
    /// serializing to JSON and deserializing back produces an equivalent record.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property AuxInstalledRecord_OptiScaler_RoundTrip_PreservesAllFields()
    {
        return Prop.ForAll(
            Arb.From(GenOptiScalerRecord),
            (AuxInstalledRecord original) =>
            {
                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<AuxInstalledRecord>(json)!;

                bool gameNameMatch = deserialized.GameName == original.GameName;
                bool installPathMatch = deserialized.InstallPath == original.InstallPath;
                bool addonTypeMatch = deserialized.AddonType == original.AddonType;
                bool installedAsMatch = deserialized.InstalledAs == original.InstalledAs;
                bool sourceUrlMatch = deserialized.SourceUrl == original.SourceUrl;
                bool remoteFileSizeMatch = deserialized.RemoteFileSize == original.RemoteFileSize;
                bool installedAtMatch = deserialized.InstalledAt == original.InstalledAt;

                return (gameNameMatch && installPathMatch && addonTypeMatch &&
                        installedAsMatch && sourceUrlMatch && remoteFileSizeMatch &&
                        installedAtMatch)
                    .Label($"gameNameMatch={gameNameMatch}, installPathMatch={installPathMatch}, " +
                           $"addonTypeMatch={addonTypeMatch}, installedAsMatch={installedAsMatch}, " +
                           $"sourceUrlMatch={sourceUrlMatch}, remoteFileSizeMatch={remoteFileSizeMatch}, " +
                           $"installedAtMatch={installedAtMatch}");
            });
    }

    /// <summary>
    /// For any valid AuxInstalledRecord with AddonType = "OptiScaler" and non-null optional fields,
    /// serializing to JSON and deserializing back preserves all fields including optional ones.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property AuxInstalledRecord_OptiScaler_RoundTrip_WithOptionalFields()
    {
        var genSourceUrl = Gen.Elements(
            "https://github.com/optiscaler/OptiScaler/releases/v0.8.0",
            "https://github.com/optiscaler/OptiScaler/releases/v0.8.1",
            "https://github.com/optiscaler/OptiScaler/releases/latest");

        var genRemoteFileSize = Gen.Choose(1_000_000, 100_000_000).Select(x => (long)x);

        var genRecordWithOptionals =
            GenGameName.SelectMany(gameName =>
            GenInstalledAs.SelectMany(installedAs =>
            genSourceUrl.SelectMany(sourceUrl =>
            genRemoteFileSize.Select(remoteFileSize =>
                new AuxInstalledRecord
                {
                    GameName = gameName,
                    InstallPath = @"C:\Games\Test",
                    AddonType = OptiScalerService.AddonType,
                    InstalledAs = installedAs,
                    SourceUrl = sourceUrl,
                    RemoteFileSize = remoteFileSize,
                    InstalledAt = DateTime.UtcNow,
                }))));

        return Prop.ForAll(
            Arb.From(genRecordWithOptionals),
            (AuxInstalledRecord original) =>
            {
                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<AuxInstalledRecord>(json)!;

                bool sourceUrlMatch = deserialized.SourceUrl == original.SourceUrl;
                bool remoteFileSizeMatch = deserialized.RemoteFileSize == original.RemoteFileSize;
                bool addonTypeMatch = deserialized.AddonType == OptiScalerService.AddonType;

                return (sourceUrlMatch && remoteFileSizeMatch && addonTypeMatch)
                    .Label($"sourceUrlMatch={sourceUrlMatch}, remoteFileSizeMatch={remoteFileSizeMatch}, " +
                           $"addonTypeMatch={addonTypeMatch}");
            });
    }
}
