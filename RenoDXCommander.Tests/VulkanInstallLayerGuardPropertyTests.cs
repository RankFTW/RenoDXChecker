using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for the InstallLayer invocation guard in MainViewModel.
/// Feature: vulkan-reshade-install-flow, Property 4: InstallLayer invoked if and only if layer is absent
/// </summary>
public class VulkanInstallLayerGuardPropertyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly bool _vulkanIniExistedBefore;

    public VulkanInstallLayerGuardPropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcLayerGuard_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);

        // Ensure inis directory exists with a Vulkan INI template so MergeRsVulkanIni won't throw
        Directory.CreateDirectory(AuxInstallService.InisDir);
        _vulkanIniExistedBefore = File.Exists(AuxInstallService.RsVulkanIniPath);
        if (!_vulkanIniExistedBefore)
            File.WriteAllText(AuxInstallService.RsVulkanIniPath, "[GENERAL]\nPerformanceMode=0\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        if (!_vulkanIniExistedBefore && File.Exists(AuxInstallService.RsVulkanIniPath))
            try { File.Delete(AuxInstallService.RsVulkanIniPath); } catch { }
    }

    // Feature: vulkan-reshade-install-flow, Property 4: InstallLayer invoked if and only if layer is absent
    /// <summary>
    /// **Validates: Requirements 3.5, 4.1**
    ///
    /// For any boolean layerInstalled, when InstallReShadeVulkanAsync executes:
    /// - If layerInstalled is true (layer present), InstallLayer() shall NOT be called.
    /// - If layerInstalled is false (layer absent), InstallLayer() SHALL be called.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property InstallLayer_CalledIffLayerIsAbsent()
    {
        var gen = Arb.Default.Bool().Generator;

        return Prop.ForAll(gen.ToArbitrary(), layerInstalled =>
        {
            var gameDir = Path.Combine(_tempRoot, "game_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(gameDir);

            try
            {
                // ── Arrange ──────────────────────────────────────────────────
                var vm = TestHelpers.CreateMainViewModel();

                // Control whether IsLayerInstalled returns true or false
                vm.IsVulkanLayerInstalledFunc = () => layerInstalled;

                // Spy: track whether InstallLayer was called
                bool installLayerCalled = false;
                vm.InstallLayerAction = () => installLayerCalled = true;

                // Pretend we're admin so the full path doesn't bail early
                vm.IsRunningAsAdminFunc = () => true;

                // Suppress the warning dialog so the full path proceeds
                vm.ShowVulkanLayerWarningDialog = () => Task.FromResult(true);

                // Create a Vulkan game card
                var card = new GameCardViewModel
                {
                    GraphicsApi = GraphicsApiType.Vulkan,
                    IsDualApiGame = false,
                    InstallPath = gameDir,
                    GameName = "TestVulkanGame",
                };

                // ── Act ──────────────────────────────────────────────────────
                vm.InstallReShadeVulkanAsync(card).GetAwaiter().GetResult();

                // ── Assert ───────────────────────────────────────────────────
                bool expected = !layerInstalled; // InstallLayer should be called iff layer is absent
                if (installLayerCalled != expected)
                    return false.Label(
                        $"layerInstalled={layerInstalled}: InstallLayer called={installLayerCalled}, expected={expected}");

                return true.Label($"OK: layerInstalled={layerInstalled}, InstallLayer called={installLayerCalled}");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }
}
