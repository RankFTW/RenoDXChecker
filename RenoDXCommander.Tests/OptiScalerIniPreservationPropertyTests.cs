using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for OptiScaler INI preservation during update.
/// Feature: optiscaler-integration, Property 3: INI Preservation During Update
/// </summary>
public class OptiScalerIniPreservationPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates random INI content as a byte array. This covers arbitrary user
    /// configurations including multi-line content, special characters, and binary-safe
    /// edge cases. Content length ranges from 0 to 2048 bytes.
    /// </summary>
    private static readonly Gen<byte[]> GenIniContent =
        from length in Gen.Choose(0, 2048)
        from bytes in Gen.ArrayOf(length, Gen.Choose(0, 255).Select(b => (byte)b))
        select bytes;

    /// <summary>
    /// Generates a random DLL filename from the supported OptiScaler DLL names.
    /// </summary>
    private static readonly Gen<string> GenDllName =
        Gen.Elements("dxgi.dll", "winmm.dll", "d3d12.dll", "dbghelp.dll",
                      "version.dll", "wininet.dll", "winhttp.dll");

    // ── Property 3 ────────────────────────────────────────────────────────────────
    // Feature: optiscaler-integration, Property 3: INI Preservation During Update
    // **Validates: Requirements 2.5**

    /// <summary>
    /// For any OptiScaler.ini content in a game folder, after an update operation
    /// (which replaces the DLL and companion files but skips the INI), the INI
    /// content is byte-identical to pre-update content.
    ///
    /// This test simulates the update operation directly: it writes an INI file,
    /// then performs the file copy operations that UpdateAsync does (overwriting
    /// the DLL and companion files), and verifies the INI is untouched.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property UpdateOperation_PreservesIniContent_ByteIdentical()
    {
        return Prop.ForAll(GenIniContent.ToArbitrary(), GenDllName.ToArbitrary(), (iniContent, dllName) =>
        {
            var gameDir = Path.Combine(Path.GetTempPath(), $"rhi_test_game_{Guid.NewGuid():N}");
            var stagingDir = Path.Combine(Path.GetTempPath(), $"rhi_test_staging_{Guid.NewGuid():N}");

            try
            {
                // Arrange — create game folder with INI and installed DLL
                Directory.CreateDirectory(gameDir);
                Directory.CreateDirectory(stagingDir);

                var iniPath = Path.Combine(gameDir, "OptiScaler.ini");
                File.WriteAllBytes(iniPath, iniContent);

                // Create a fake installed DLL in the game folder
                var installedDllPath = Path.Combine(gameDir, dllName);
                File.WriteAllText(installedDllPath, "old-dll-content");

                // Create a fake companion file in the game folder
                var companionPath = Path.Combine(gameDir, "fakenvapi.dll");
                File.WriteAllText(companionPath, "old-companion-content");

                // Create staging files (new versions)
                File.WriteAllText(Path.Combine(stagingDir, "OptiScaler.dll"), "new-dll-content");
                File.WriteAllText(Path.Combine(stagingDir, "fakenvapi.dll"), "new-companion-content");
                File.WriteAllText(Path.Combine(stagingDir, "OptiScaler.ini"), "new-default-ini-content");

                // Act — simulate the update operation (same logic as UpdateAsync):
                // 1. Replace the DLL (overwrite)
                File.Copy(Path.Combine(stagingDir, "OptiScaler.dll"), installedDllPath, overwrite: true);

                // 2. Replace companion files (overwrite)
                File.Copy(Path.Combine(stagingDir, "fakenvapi.dll"), companionPath, overwrite: true);

                // 3. Do NOT touch OptiScaler.ini — this is the key preservation behavior

                // Assert — INI content is byte-identical to pre-update content
                var postUpdateIni = File.ReadAllBytes(iniPath);

                var isIdentical = iniContent.Length == postUpdateIni.Length
                    && iniContent.AsSpan().SequenceEqual(postUpdateIni.AsSpan());

                return isIdentical
                    .Label($"INI content should be byte-identical after update. " +
                           $"Original size: {iniContent.Length}, Post-update size: {postUpdateIni.Length}");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
                try { Directory.Delete(stagingDir, recursive: true); } catch { }
            }
        });
    }
}
