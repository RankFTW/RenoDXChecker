using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for ReShade filename restoration priority on OptiScaler uninstall.
/// Feature: optiscaler-integration, Property 9: ReShade Filename Restoration Priority on Uninstall
/// </summary>
public class OptiScalerReShadeRestorePriorityPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a nullable DLL filename string representing a user override.
    /// null or empty means no user override is set.
    /// </summary>
    private static readonly Gen<string?> GenUserOverride =
        Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            Gen.Elements<string?>("d3d9.dll", "d3d11.dll", "opengl32.dll", "dxgi.dll", "version.dll", "winmm.dll")
        );

    /// <summary>
    /// Generates a nullable DLL filename string representing a manifest override.
    /// null or empty means no manifest override is set.
    /// </summary>
    private static readonly Gen<string?> GenManifestOverride =
        Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            Gen.Elements<string?>("d3d9.dll", "d3d11.dll", "opengl32.dll", "dxgi.dll", "dinput8.dll")
        );

    /// <summary>
    /// Generates a nullable detected graphics API string.
    /// null means no API was detected.
    /// </summary>
    private static readonly Gen<string?> GenDetectedApi =
        Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            Gen.Elements<string?>("dx9", "directx9", "dx11", "dx12", "directx11", "directx12", "opengl", "vulkan")
        );

    // ── Property 9 ────────────────────────────────────────────────────────────────
    // Feature: optiscaler-integration, Property 9: ReShade Filename Restoration Priority on Uninstall
    // **Validates: Requirements 5.4**

    /// <summary>
    /// For any combination of user DLL override, manifest override, and detected graphics API,
    /// the restored ReShade filename follows the priority:
    /// user override > manifest override > API-based default > dxgi.dll.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property ReShadeFilename_FollowsPriorityChain_OnRestore()
    {
        return Prop.ForAll(
            GenUserOverride.ToArbitrary(),
            GenManifestOverride.ToArbitrary(),
            GenDetectedApi.ToArbitrary(),
            (userOverride, manifestOverride, detectedApi) =>
            {
                // Act — call the static resolution method
                var result = OptiScalerService.ResolveReShadeFilename(userOverride, manifestOverride, detectedApi);

                // Compute expected result following the priority chain
                string expected;
                if (!string.IsNullOrWhiteSpace(userOverride))
                {
                    expected = userOverride!;
                }
                else if (!string.IsNullOrWhiteSpace(manifestOverride))
                {
                    expected = manifestOverride!;
                }
                else if (!string.IsNullOrWhiteSpace(detectedApi))
                {
                    expected = detectedApi!.ToLowerInvariant() switch
                    {
                        "dx9" or "directx9" => "d3d9.dll",
                        "opengl" => "opengl32.dll",
                        "dx11" or "dx12" or "directx11" or "directx12" => "dxgi.dll",
                        _ => AuxInstallService.RsNormalName,
                    };
                }
                else
                {
                    expected = AuxInstallService.RsNormalName; // dxgi.dll
                }

                return result.Equals(expected, StringComparison.Ordinal)
                    .Label($"Expected '{expected}' but got '{result}'. " +
                           $"userOverride='{userOverride ?? "(null)"}', " +
                           $"manifestOverride='{manifestOverride ?? "(null)"}', " +
                           $"detectedApi='{detectedApi ?? "(null)"}'");
            });
    }

    /// <summary>
    /// When all overrides are null/empty, the result is always dxgi.dll (the default).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReShadeFilename_DefaultsToDxgiDll_WhenNoOverrides()
    {
        return Prop.ForAll(
            Gen.Constant<string?>(null).ToArbitrary(),
            Gen.Constant<string?>(null).ToArbitrary(),
            Gen.Constant<string?>(null).ToArbitrary(),
            (userOverride, manifestOverride, detectedApi) =>
            {
                var result = OptiScalerService.ResolveReShadeFilename(userOverride, manifestOverride, detectedApi);
                return result.Equals(AuxInstallService.RsNormalName, StringComparison.Ordinal)
                    .Label($"Expected '{AuxInstallService.RsNormalName}' but got '{result}'");
            });
    }

    /// <summary>
    /// User override always takes precedence over manifest and API.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property UserOverride_AlwaysTakesPrecedence()
    {
        var genNonEmptyOverride = Gen.Elements("d3d9.dll", "d3d11.dll", "opengl32.dll", "version.dll", "winmm.dll");

        return Prop.ForAll(
            genNonEmptyOverride.ToArbitrary(),
            GenManifestOverride.ToArbitrary(),
            GenDetectedApi.ToArbitrary(),
            (userOverride, manifestOverride, detectedApi) =>
            {
                var result = OptiScalerService.ResolveReShadeFilename(userOverride, manifestOverride, detectedApi);
                return result.Equals(userOverride, StringComparison.Ordinal)
                    .Label($"Expected user override '{userOverride}' but got '{result}'. " +
                           $"manifestOverride='{manifestOverride ?? "(null)"}', detectedApi='{detectedApi ?? "(null)"}'");
            });
    }
}
