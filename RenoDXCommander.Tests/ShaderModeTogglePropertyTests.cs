using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for shader mode toggle state behaviour.
/// </summary>
public class ShaderModeTogglePropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>Generates a non-empty, non-whitespace game name string.</summary>
    private static readonly Gen<string> GenGameName =
        Arb.Default.NonEmptyString().Generator
           .Select(nes => nes.Get)
           .Where(s => !string.IsNullOrWhiteSpace(s));

    // ── Property 4 ────────────────────────────────────────────────────────────────
    // Feature: override-menu-redesign, Property 4: Shader mode reflects Global toggle state
    // **Validates: Requirements 4.4**

    [Property(MaxTest = 100)]
    public Property ShaderMode_GlobalToggle_ReflectsState()
    {
        return Prop.ForAll(
            Arb.From(GenGameName),
            (string gameName) =>
            {
                var vm = TestHelpers.CreateMainViewModel();

                // Set Global toggle ON → mode should be "Global"
                vm.SetPerGameShaderMode(gameName, "Global");
                bool globalOn = vm.GetPerGameShaderMode(gameName) == "Global";

                // Set Global toggle OFF (Select) → mode should be "Select"
                vm.SetPerGameShaderMode(gameName, "Select");
                bool selectOn = vm.GetPerGameShaderMode(gameName) == "Select";

                return (globalOn && selectOn)
                    .Label($"gameName='{gameName}', globalOn={globalOn}, selectOn={selectOn}");
            });
    }

    // ── Property 5 ────────────────────────────────────────────────────────────────
    // Feature: override-menu-redesign, Property 5: Shader mode reflects Custom toggle state
    // **Validates: Requirements 4.5**

    [Property(MaxTest = 100)]
    public Property ShaderMode_CustomToggle_ReflectsState()
    {
        return Prop.ForAll(
            Arb.From(GenGameName),
            (string gameName) =>
            {
                var vm = TestHelpers.CreateMainViewModel();

                // Set Custom toggle ON → mode should be "Custom"
                vm.SetPerGameShaderMode(gameName, "Custom");
                bool customOn = vm.GetPerGameShaderMode(gameName) == "Custom";

                // Set Custom toggle OFF → mode reverts to "Global"
                vm.SetPerGameShaderMode(gameName, "Global");
                bool globalReverted = vm.GetPerGameShaderMode(gameName) == "Global";

                return (customOn && globalReverted)
                    .Label($"gameName='{gameName}', customOn={customOn}, globalReverted={globalReverted}");
            });
    }
}
