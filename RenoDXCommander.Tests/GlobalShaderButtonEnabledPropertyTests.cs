using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for global shader button enabled state.
/// Feature: custom-shaders, Property 2: Global shader button enabled state is the inverse of custom shaders
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
public class GlobalShaderButtonEnabledPropertyTests
{
    /// <summary>
    /// For any value of UseCustomShaders (true or false), the computed property
    /// IsGlobalShaderButtonEnabled shall equal !UseCustomShaders. Changing
    /// UseCustomShaders shall immediately update IsGlobalShaderButtonEnabled
    /// without requiring any additional action.
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IsGlobalShaderButtonEnabled_IsInverseOfUseCustomShaders()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            (bool useCustomShaders) =>
            {
                // Arrange: create MainViewModel via TestHelpers
                var vm = TestHelpers.CreateMainViewModel();

                // Act: set UseCustomShaders on the settings
                vm.Settings.IsLoadingSettings = true;
                vm.Settings.UseCustomShaders = useCustomShaders;
                vm.Settings.IsLoadingSettings = false;

                // Assert: IsGlobalShaderButtonEnabled is the inverse
                var expected = !useCustomShaders;
                var actual = vm.IsGlobalShaderButtonEnabled;

                if (actual != expected)
                    return false.Label(
                        $"IsGlobalShaderButtonEnabled mismatch: UseCustomShaders={useCustomShaders}, " +
                        $"expected={expected}, actual={actual}");

                return true.Label(
                    $"OK: UseCustomShaders={useCustomShaders} → IsGlobalShaderButtonEnabled={actual}");
            });
    }
}
