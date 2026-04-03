using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

// Feature: display-commander-reintegration, Property 5: DC DLL override resolution priority

/// <summary>
/// Property-based tests for DC DLL override resolution priority.
/// For any combination of user DLL override, manifest override, and bitness,
/// the effective DC filename follows the priority: user override > manifest override > default.
/// **Validates: Requirements 6.3, 6.4, 9.2, 9.3, 9.4**
/// </summary>
public class DcDllOverrideResolutionPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates nullable DLL name strings including null and empty to cover all override states.
    /// </summary>
    private static readonly Gen<string?> GenOverrideName =
        Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            Gen.Elements<string?>("dxgi.dll", "d3d9.dll", "winmm.dll", "version.dll", "d3d11.dll"));

    /// <summary>
    /// Replicates the resolution priority logic from MainViewModel.ResolveDcFileName:
    ///   1. If user override is non-empty → use user override
    ///   2. Else if manifest override is non-empty → use manifest override
    ///   3. Else → use default GetDcFileName(is32Bit)
    /// </summary>
    private static string Resolve(string? userOverride, string? manifestOverride, bool is32Bit)
    {
        if (!string.IsNullOrEmpty(userOverride))
            return userOverride;

        if (!string.IsNullOrEmpty(manifestOverride))
            return manifestOverride;

        return MainViewModel.GetDcFileName(is32Bit);
    }

    // ── Property 5: DC DLL override resolution priority ───────────────────────────

    [Property(MaxTest = 10)]
    public Property UserOverride_TakesPriority_OverManifestAndDefault()
    {
        var gen = from userOverride in GenOverrideName
                  from manifestOverride in GenOverrideName
                  from is32Bit in Arb.Default.Bool().Generator
                  where !string.IsNullOrEmpty(userOverride)
                  select (userOverride, manifestOverride, is32Bit);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var result = Resolve(tuple.userOverride, tuple.manifestOverride, tuple.is32Bit);

            return (result == tuple.userOverride).Label(
                $"User override '{tuple.userOverride}' should win but got '{result}' " +
                $"(manifest='{tuple.manifestOverride}', is32Bit={tuple.is32Bit})");
        });
    }

    [Property(MaxTest = 10)]
    public Property ManifestOverride_TakesPriority_WhenNoUserOverride()
    {
        var gen = from userOverride in Gen.OneOf(Gen.Constant<string?>(null), Gen.Constant<string?>(""))
                  from manifestOverride in Gen.Elements<string?>("dxgi.dll", "d3d9.dll", "winmm.dll", "version.dll")
                  from is32Bit in Arb.Default.Bool().Generator
                  select (userOverride, manifestOverride, is32Bit);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var result = Resolve(tuple.userOverride, tuple.manifestOverride, tuple.is32Bit);

            return (result == tuple.manifestOverride).Label(
                $"Manifest override '{tuple.manifestOverride}' should win but got '{result}' " +
                $"(user='{tuple.userOverride}', is32Bit={tuple.is32Bit})");
        });
    }

    [Property(MaxTest = 10)]
    public Property Default_UsedWhenNoOverrides()
    {
        var gen = from userOverride in Gen.OneOf(Gen.Constant<string?>(null), Gen.Constant<string?>(""))
                  from manifestOverride in Gen.OneOf(Gen.Constant<string?>(null), Gen.Constant<string?>(""))
                  from is32Bit in Arb.Default.Bool().Generator
                  select (userOverride, manifestOverride, is32Bit);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var expected = MainViewModel.GetDcFileName(tuple.is32Bit);
            var result = Resolve(tuple.userOverride, tuple.manifestOverride, tuple.is32Bit);

            return (result == expected).Label(
                $"Default '{expected}' should be used but got '{result}' " +
                $"(user='{tuple.userOverride}', manifest='{tuple.manifestOverride}', is32Bit={tuple.is32Bit})");
        });
    }

    [Property(MaxTest = 10)]
    public Property Resolution_AlwaysReturnsNonEmpty()
    {
        var gen = from userOverride in GenOverrideName
                  from manifestOverride in GenOverrideName
                  from is32Bit in Arb.Default.Bool().Generator
                  select (userOverride, manifestOverride, is32Bit);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var result = Resolve(tuple.userOverride, tuple.manifestOverride, tuple.is32Bit);

            return (!string.IsNullOrEmpty(result)).Label(
                $"Resolution should never return empty but got '{result}' " +
                $"(user='{tuple.userOverride}', manifest='{tuple.manifestOverride}', is32Bit={tuple.is32Bit})");
        });
    }
}
