// Feature: optiscaler-integration, Property 5: DLL Naming Resolution Priority Chain
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for OptiScaler DLL naming resolution priority chain.
/// Uses FsCheck with xUnit.
///
/// **Validates: Requirements 3.4, 9.6, 19.3**
///
/// For any game name, given a user DLL override (string or null), a manifest
/// override (string or null), and default dxgi.dll, the resolved name follows:
/// user override if non-empty → manifest override if non-empty → dxgi.dll.
/// </summary>
public class OptiScalerDllNamingPriorityPropertyTests : IDisposable
{
    private static readonly string[] SupportedNames = OptiScalerService.SupportedDllNames;

    private readonly DllOverrideService _service;

    public OptiScalerDllNamingPriorityPropertyTests()
    {
        var auxInstaller = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
        _service = new DllOverrideService(auxInstaller);
    }

    public void Dispose() { }

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a game name as a non-empty alphanumeric string.
    /// </summary>
    private static Gen<string> GenGameName()
    {
        return Gen.Elements(
            "Cyberpunk 2077", "Elden Ring", "Starfield",
            "Baldurs Gate 3", "Hogwarts Legacy", "TestGame_" + Guid.NewGuid().ToString("N")[..6]);
    }

    /// <summary>
    /// Generates an optional DLL name: either a supported name or null/empty (meaning "not set").
    /// </summary>
    private static Gen<string?> GenOptionalDllName()
    {
        var genSupported = Gen.Elements(SupportedNames).Select<string, string?>(s => s);
        var genEmpty = Gen.Elements<string?>(null, "", "  ");
        return Gen.Frequency(
            Tuple.Create(3, genSupported),
            Tuple.Create(2, genEmpty));
    }

    // ── Property 5: DLL Naming Resolution Priority Chain ──────────────────────────

    /// <summary>
    /// Property 5: DLL Naming Resolution Priority Chain
    ///
    /// **Validates: Requirements 3.4, 9.6, 19.3**
    ///
    /// For any game name, given a user DLL override (string or null), a manifest
    /// override (string or null), and default dxgi.dll, the resolved name follows:
    /// user override if non-empty, else manifest override if non-empty, else dxgi.dll.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetEffectiveOsName_FollowsPriorityChain()
    {
        return Prop.ForAll(
            Arb.From(GenGameName()),
            Arb.From(GenOptionalDllName()),
            Arb.From(GenOptionalDllName()),
            (string gameName, string? userOverride, string? manifestOverride) =>
            {
                // Arrange: fresh service per test case to avoid cross-contamination
                var auxInstaller = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
                var service = new DllOverrideService(auxInstaller);

                // Set up manifest override if provided
                if (!string.IsNullOrWhiteSpace(manifestOverride))
                {
                    service.LoadManifestOsDllOverrides(
                        new Dictionary<string, string> { { gameName, manifestOverride } });
                }
                else
                {
                    service.LoadManifestOsDllOverrides(null);
                }

                // Set up user override if provided
                if (!string.IsNullOrWhiteSpace(userOverride))
                {
                    service.SetOsDllOverride(gameName, userOverride);
                }

                // Act
                var result = service.GetEffectiveOsName(gameName);

                // Assert: priority chain
                string expected;
                if (!string.IsNullOrWhiteSpace(userOverride))
                    expected = userOverride.Trim();
                else if (!string.IsNullOrWhiteSpace(manifestOverride))
                    expected = manifestOverride;
                else
                    expected = OptiScalerService.DefaultDllName;

                return (result == expected)
                    .Label($"gameName='{gameName}', userOverride='{userOverride}', " +
                           $"manifestOverride='{manifestOverride}' => " +
                           $"result='{result}', expected='{expected}'");
            });
    }

    /// <summary>
    /// When no overrides are set at all, GetEffectiveOsName returns the default dxgi.dll.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GetEffectiveOsName_NoOverrides_ReturnsDefault()
    {
        return Prop.ForAll(
            Arb.From(GenGameName()),
            (string gameName) =>
            {
                var auxInstaller = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
                var service = new DllOverrideService(auxInstaller);

                var result = service.GetEffectiveOsName(gameName);

                return (result == OptiScalerService.DefaultDllName)
                    .Label($"gameName='{gameName}' => result='{result}', " +
                           $"expected='{OptiScalerService.DefaultDllName}'");
            });
    }

    /// <summary>
    /// User override always takes precedence over manifest override.
    ///
    /// **Validates: Requirements 9.6, 19.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GetEffectiveOsName_UserOverride_TakesPrecedenceOverManifest()
    {
        var genDistinctPair = from idx1 in Gen.Choose(0, SupportedNames.Length - 1)
                              from idx2 in Gen.Choose(0, SupportedNames.Length - 1)
                                            .Where(i => i != idx1)
                              select (SupportedNames[idx1], SupportedNames[idx2]);

        return Prop.ForAll(
            Arb.From(GenGameName()),
            Arb.From(genDistinctPair),
            (string gameName, (string userDll, string manifestDll) pair) =>
            {
                var auxInstaller = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
                var service = new DllOverrideService(auxInstaller);

                // Set both overrides
                service.LoadManifestOsDllOverrides(
                    new Dictionary<string, string> { { gameName, pair.manifestDll } });
                service.SetOsDllOverride(gameName, pair.userDll);

                var result = service.GetEffectiveOsName(gameName);

                return (result == pair.userDll)
                    .Label($"gameName='{gameName}', user='{pair.userDll}', " +
                           $"manifest='{pair.manifestDll}' => result='{result}', " +
                           $"expected user override='{pair.userDll}'");
            });
    }
}
