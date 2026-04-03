using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for DllOverrideConfig and ManifestDllNames JSON round-trip.
/// Feature: display-commander-reintegration, Property 4: DllOverrideConfig and ManifestDllNames round-trip
/// </summary>
public class DcDllOverrideRoundTripPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates DLL name strings including null, matching typical override values.
    /// </summary>
    private static readonly Gen<string?> GenDllName =
        Gen.Elements<string?>("dxgi.dll", "d3d9.dll", "winmm.dll", "version.dll", null);

    /// <summary>
    /// Generates non-null DLL name strings for DllOverrideConfig (which uses "" as default, not null).
    /// </summary>
    private static readonly Gen<string> GenNonNullDllName =
        Gen.Elements("dxgi.dll", "d3d9.dll", "winmm.dll", "version.dll", "");

    // ── Property 4a: DllOverrideConfig round-trip ─────────────────────────────────
    // Feature: display-commander-reintegration, Property 4: DllOverrideConfig and ManifestDllNames round-trip
    // **Validates: Requirements 6.5, 9.1**

    /// <summary>
    /// For any valid DllOverrideConfig with arbitrary ReShadeFileName and DcFileName strings,
    /// serializing to JSON and deserializing back shall produce an equivalent object.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property DllOverrideConfig_RoundTrip_PreservesBothFileNames()
    {
        return Prop.ForAll(
            Arb.From(GenNonNullDllName),
            Arb.From(GenNonNullDllName),
            (string rsName, string dcName) =>
            {
                var original = new DllOverrideConfig
                {
                    ReShadeFileName = rsName,
                    DcFileName = dcName
                };

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<DllOverrideConfig>(json)!;

                bool rsMatch = deserialized.ReShadeFileName == original.ReShadeFileName;
                bool dcMatch = deserialized.DcFileName == original.DcFileName;

                return (rsMatch && dcMatch)
                    .Label($"rsMatch={rsMatch}, dcMatch={dcMatch} " +
                           $"(rs='{rsName}', dc='{dcName}', json='{json}')");
            });
    }

    // ── Property 4b: ManifestDllNames round-trip ──────────────────────────────────
    // Feature: display-commander-reintegration, Property 4: DllOverrideConfig and ManifestDllNames round-trip
    // **Validates: Requirements 6.5, 9.1**

    /// <summary>
    /// For any ManifestDllNames with arbitrary ReShade and Dc strings (including null),
    /// serializing to JSON and deserializing back shall preserve both fields.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property ManifestDllNames_RoundTrip_PreservesBothFields()
    {
        return Prop.ForAll(
            Arb.From(GenDllName),
            Arb.From(GenDllName),
            (string? reshade, string? dc) =>
            {
                var original = new ManifestDllNames
                {
                    ReShade = reshade,
                    Dc = dc
                };

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<ManifestDllNames>(json)!;

                bool rsMatch = deserialized.ReShade == original.ReShade;
                bool dcMatch = deserialized.Dc == original.Dc;

                return (rsMatch && dcMatch)
                    .Label($"rsMatch={rsMatch}, dcMatch={dcMatch} " +
                           $"(reshade='{reshade}', dc='{dc}', json='{json}')");
            });
    }
}
