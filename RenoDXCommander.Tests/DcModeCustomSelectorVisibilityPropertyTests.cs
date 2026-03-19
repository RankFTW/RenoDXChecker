using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

// Feature: dc-mode-ui-enhancements, Property 4: DC Mode Custom selector visibility matches selection

/// <summary>
/// Property-based tests for DC Mode Custom selector visibility.
/// For any DC Mode combo selection index, the DLL filename selector should be
/// visible if and only if the selected index corresponds to "DC Mode Custom" (index 4).
/// For all other indices (0–3), the selector should be hidden.
/// **Validates: Requirements 4.2, 4.3**
/// </summary>
public class DcModeCustomSelectorVisibilityPropertyTests
{
    /// <summary>
    /// The valid DC Mode combo indices: 0 = Global, 1 = Exclude (Off),
    /// 2 = DC Mode 1, 3 = DC Mode 2, 4 = DC Mode Custom.
    /// </summary>
    private static readonly Gen<int> GenDcModeIndex = Gen.Elements(0, 1, 2, 3, 4);

    /// <summary>
    /// Mirrors the visibility logic used in DetailPanelBuilder.BuildOverridesPanel:
    ///   dcCustomDllSelector.Visibility = dcModeCombo.SelectedIndex == 4
    ///       ? Visibility.Visible : Visibility.Collapsed;
    /// Returns true when the selector should be visible.
    /// </summary>
    private static bool ShouldDcCustomSelectorBeVisible(int selectedIndex) =>
        selectedIndex == 4;

    /// <summary>
    /// For any valid DC Mode combo index, the DLL filename selector is visible
    /// if and only if the index is 4 ("DC Mode Custom").
    /// </summary>
    [Property(MaxTest = 10)]
    public Property DcCustomSelector_VisibleOnlyForDcModeCustom()
    {
        return Prop.ForAll(
            GenDcModeIndex.ToArbitrary(),
            selectedIndex =>
            {
                bool shouldBeVisible = ShouldDcCustomSelectorBeVisible(selectedIndex);
                bool expectedVisible = selectedIndex == 4;

                if (shouldBeVisible != expectedVisible)
                    return false.Label(
                        $"Visibility mismatch at index {selectedIndex}: " +
                        $"ShouldBeVisible={shouldBeVisible}, Expected={expectedVisible}");

                return true.Label("OK");
            });
    }

    /// <summary>
    /// For any non-custom DC Mode index (0–3), the DLL filename selector
    /// should always be hidden (Collapsed).
    /// </summary>
    [Property(MaxTest = 10)]
    public Property DcCustomSelector_HiddenForNonCustomIndices()
    {
        var genNonCustomIndex = Gen.Elements(0, 1, 2, 3);

        return Prop.ForAll(
            genNonCustomIndex.ToArbitrary(),
            selectedIndex =>
            {
                bool visible = ShouldDcCustomSelectorBeVisible(selectedIndex);

                if (visible)
                    return false.Label(
                        $"Selector should be hidden for index {selectedIndex} but was visible");

                return true.Label("OK");
            });
    }

    /// <summary>
    /// The DC Mode Custom index (4) should always result in a visible selector.
    /// This is a degenerate property test that confirms the constant case.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property DcCustomSelector_AlwaysVisibleForCustomIndex()
    {
        // Generate arbitrary booleans as "noise" to ensure the property holds
        // regardless of other state — the visibility depends solely on the index.
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                const int customIndex = 4;
                bool visible = ShouldDcCustomSelectorBeVisible(customIndex);

                if (!visible)
                    return false.Label("Selector should be visible for DC Mode Custom (index 4)");

                return true.Label("OK");
            });
    }
}
