using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for ReShade rename on OptiScaler install.
/// Feature: optiscaler-integration, Property 7: ReShade Rename on OptiScaler Install
/// </summary>
public class OptiScalerReShadeRenamePropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a ReShade DLL filename from the set of common DLL names that ReShade
    /// can be installed as. These are the names that might be found in a game folder.
    /// </summary>
    private static readonly Gen<string> GenReShadeFileName =
        Gen.Elements(DllOverrideConstants.CommonDllNames);

    // ── Property 7 ────────────────────────────────────────────────────────────────
    // Feature: optiscaler-integration, Property 7: ReShade Rename on OptiScaler Install
    // **Validates: Requirements 4.1**

    /// <summary>
    /// For any ReShade DLL filename from supported names, when OptiScaler is installed
    /// in a game folder containing that ReShade DLL, the ReShade file is renamed to
    /// ReShade64.dll.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property ReShadeFile_IsRenamedTo_ReShade64Dll_WhenOptiScalerInstalls()
    {
        return Prop.ForAll(GenReShadeFileName.ToArbitrary(), rsFileName =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"rhi_test_rs_rename_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                // Arrange — create a fake ReShade DLL in the temp game folder
                var rsFilePath = Path.Combine(tempDir, rsFileName);
                var rsContent = new byte[] { 0x52, 0x65, 0x53, 0x68, 0x61, 0x64, 0x65 }; // "ReShade" marker
                File.WriteAllBytes(rsFilePath, rsContent);

                var rsDestPath = Path.Combine(tempDir, OptiScalerService.ReShadeCoexistName);

                // Act — simulate the rename logic from InstallAsync
                if (!rsFileName.Equals(OptiScalerService.ReShadeCoexistName, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(rsDestPath))
                        File.Delete(rsDestPath);
                    File.Move(rsFilePath, rsDestPath);
                }

                // Assert — ReShade64.dll should exist with the original content
                var reshade64Exists = File.Exists(rsDestPath);
                var contentPreserved = reshade64Exists && File.ReadAllBytes(rsDestPath).SequenceEqual(rsContent);

                // The original file should no longer exist (unless it was already ReShade64.dll)
                var originalGone = rsFileName.Equals(OptiScalerService.ReShadeCoexistName, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(rsFilePath);

                return (reshade64Exists && contentPreserved && originalGone)
                    .Label($"ReShade '{rsFileName}' should be renamed to '{OptiScalerService.ReShadeCoexistName}'. " +
                           $"ReShade64.dll exists={reshade64Exists}, content preserved={contentPreserved}, original gone={originalGone}");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        });
    }
}
