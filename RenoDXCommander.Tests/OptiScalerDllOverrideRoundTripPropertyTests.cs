using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for DllOverrideConfig JSON round-trip including OsFileName.
/// Feature: optiscaler-integration, Property 10: DllOverrideConfig Round-Trip
/// </summary>
public class OptiScalerDllOverrideRoundTripPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates non-null DLL name strings for DllOverrideConfig (which uses "" as default, not null).
    /// Includes typical override values and the empty default.
    /// </summary>
    private static readonly Gen<string> GenNonNullDllName =
        Gen.Elements("dxgi.dll", "d3d9.dll", "winmm.dll", "d3d12.dll",
                      "dbghelp.dll", "version.dll", "wininet.dll", "winhttp.dll", "");

    // ── Property 10: DllOverrideConfig round-trip with OsFileName ─────────────────
    // Feature: optiscaler-integration, Property 10: DllOverrideConfig Round-Trip
    // **Validates: Requirements 9.1, 9.2**

    /// <summary>
    /// For any valid DllOverrideConfig with arbitrary ReShadeFileName, DcFileName, and OsFileName
    /// strings, serializing to JSON and deserializing back shall produce an equivalent object.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property DllOverrideConfig_RoundTrip_PreservesAllThreeFileNames()
    {
        return Prop.ForAll(
            Arb.From(GenNonNullDllName),
            Arb.From(GenNonNullDllName),
            Arb.From(GenNonNullDllName),
            (string rsName, string dcName, string osName) =>
            {
                var original = new DllOverrideConfig
                {
                    ReShadeFileName = rsName,
                    DcFileName = dcName,
                    OsFileName = osName
                };

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<DllOverrideConfig>(json)!;

                bool rsMatch = deserialized.ReShadeFileName == original.ReShadeFileName;
                bool dcMatch = deserialized.DcFileName == original.DcFileName;
                bool osMatch = deserialized.OsFileName == original.OsFileName;

                return (rsMatch && dcMatch && osMatch)
                    .Label($"rsMatch={rsMatch}, dcMatch={dcMatch}, osMatch={osMatch} " +
                           $"(rs='{rsName}', dc='{dcName}', os='{osName}', json='{json}')");
            });
    }
}
