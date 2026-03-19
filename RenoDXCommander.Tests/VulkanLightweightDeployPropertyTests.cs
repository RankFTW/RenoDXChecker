using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for lightweight Vulkan ReShade deploy file creation.
/// Feature: vulkan-reshade-install-flow, Property 3: Lightweight deploy creates all expected files
/// </summary>
public class VulkanLightweightDeployPropertyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly bool _vulkanIniExistedBefore;
    private readonly bool _presetExistedBefore;

    public VulkanLightweightDeployPropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcLightDeploy_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);

        // Ensure inis directory exists
        Directory.CreateDirectory(AuxInstallService.InisDir);

        // Track pre-existing state so we only clean up what we created
        _vulkanIniExistedBefore = File.Exists(AuxInstallService.RsVulkanIniPath);
        _presetExistedBefore = File.Exists(AuxInstallService.RsPresetIniPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }

        // Clean up inis files only if we created them
        if (!_vulkanIniExistedBefore && File.Exists(AuxInstallService.RsVulkanIniPath))
            try { File.Delete(AuxInstallService.RsVulkanIniPath); } catch { }
        if (!_presetExistedBefore && File.Exists(AuxInstallService.RsPresetIniPath))
            try { File.Delete(AuxInstallService.RsPresetIniPath); } catch { }
    }

    // Feature: vulkan-reshade-install-flow, Property 3: Lightweight deploy creates all expected files
    /// <summary>
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 4.3**
    ///
    /// For any combination of (presetExists, vulkanIniTemplateExists), when the
    /// lightweight deploy steps execute, the game directory shall contain:
    /// - reshade.ini (merged from the Vulkan INI template)
    /// - RDXC_VULKAN_FOOTPRINT (the footprint marker)
    /// - ReShadePreset.ini if and only if the preset file existed in the inis directory
    /// </summary>
    [Property(MaxTest = 10)]
    public Property LightweightDeploy_CreatesAllExpectedFiles()
    {
        var gen = from presetExists in Arb.Default.Bool().Generator
                  from vulkanIniTemplateExists in Arb.Default.Bool().Generator
                  select (presetExists, vulkanIniTemplateExists);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (presetExists, vulkanIniTemplateExists) = tuple;

            var gameDir = Path.Combine(_tempRoot, "game_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(gameDir);

            try
            {
                // ── Arrange: set up inis directory state ──────────────────────
                // Always ensure at least one INI template exists so MergeRsVulkanIni won't throw.
                // If vulkanIniTemplateExists is true, create the Vulkan-specific template;
                // otherwise ensure the fallback reshade.ini exists.
                if (vulkanIniTemplateExists)
                {
                    if (!File.Exists(AuxInstallService.RsVulkanIniPath))
                        File.WriteAllText(AuxInstallService.RsVulkanIniPath, "[GENERAL]\nPerformanceMode=0\n");
                }
                else
                {
                    // Remove Vulkan template so fallback is used
                    if (File.Exists(AuxInstallService.RsVulkanIniPath))
                        File.Delete(AuxInstallService.RsVulkanIniPath);
                    // Ensure fallback reshade.ini exists
                    if (!File.Exists(AuxInstallService.RsIniPath))
                        File.WriteAllText(AuxInstallService.RsIniPath, "[GENERAL]\nPerformanceMode=0\n");
                }

                if (presetExists)
                {
                    File.WriteAllText(AuxInstallService.RsPresetIniPath, "[GENERAL]\nPresetPath=test\n");
                }
                else
                {
                    if (File.Exists(AuxInstallService.RsPresetIniPath))
                        File.Delete(AuxInstallService.RsPresetIniPath);
                }

                // ── Act: invoke lightweight deploy steps ─────────────────────
                AuxInstallService.MergeRsVulkanIni(gameDir);
                AuxInstallService.CopyRsPresetIniIfPresent(gameDir);
                VulkanFootprintService.Create(gameDir);

                // ── Assert ───────────────────────────────────────────────────
                var reshadeIniExists = File.Exists(Path.Combine(gameDir, "reshade.ini"));
                if (!reshadeIniExists)
                    return false.Label(
                        $"reshade.ini missing (vulkanTemplate={vulkanIniTemplateExists}, preset={presetExists})");

                var footprintExists = File.Exists(Path.Combine(gameDir, VulkanFootprintService.FootprintFileName));
                if (!footprintExists)
                    return false.Label(
                        $"RDXC_VULKAN_FOOTPRINT missing (vulkanTemplate={vulkanIniTemplateExists}, preset={presetExists})");

                var presetIniExists = File.Exists(Path.Combine(gameDir, "ReShadePreset.ini"));
                if (presetExists != presetIniExists)
                    return false.Label(
                        $"ReShadePreset.ini expected={presetExists}, actual={presetIniExists} " +
                        $"(vulkanTemplate={vulkanIniTemplateExists})");

                return true.Label(
                    $"OK: vulkanTemplate={vulkanIniTemplateExists}, preset={presetExists}");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }
}
