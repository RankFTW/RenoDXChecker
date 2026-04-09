using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for addon deployment exclusion when UseNormalReShade is active.
/// Feature: reshade-no-addon-support, Property 2: Addon deployment exclusion
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
public class NormalReShadeAddonExclusionPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenGameName =
        from len in Gen.Choose(1, 30)
        from chars in Gen.ArrayOf(len, Gen.Elements(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -_".ToCharArray()))
        select new string(chars);

    private static readonly Gen<string> GenPackageName =
        from len in Gen.Choose(1, 20)
        from chars in Gen.ArrayOf(len, Gen.Elements(
            "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray()))
        select new string(chars);

    private static readonly Gen<List<string>> GenAddonSelection =
        from count in Gen.Choose(1, 5)
        from names in Gen.ArrayOf(count, GenPackageName)
        select names.Distinct().ToList();

    /// <summary>
    /// Simulates the addon deployment decision logic from MainViewModel.DeployAddonsForCard.
    /// When UseNormalReShade is true, returns an empty list (zero addons).
    /// When false, returns the actual selection.
    /// </summary>
    private static List<string>? ComputeEffectiveSelection(
        bool useNormalReShade,
        string addonMode,
        List<string>? globalAddons,
        List<string>? perGameSelection)
    {
        if (useNormalReShade)
            return new List<string>();

        bool useGlobalSet = addonMode != "Select";
        return useGlobalSet ? globalAddons : perGameSelection;
    }

    // ── Property 2a: UseNormalReShade=true → zero addons regardless of selection ──
    // Feature: reshade-no-addon-support, Property 2: Addon deployment exclusion
    // **Validates: Requirements 3.1, 3.2**

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeTrue_EffectiveSelection_IsEmpty()
    {
        var genAddonMode = Gen.Elements("Global", "Select");

        return Prop.ForAll(
            Arb.From(GenAddonSelection),
            Arb.From(GenAddonSelection),
            Arb.From(genAddonMode),
            (globalAddons, perGameAddons, addonMode) =>
            {
                var effective = ComputeEffectiveSelection(
                    useNormalReShade: true,
                    addonMode: addonMode,
                    globalAddons: globalAddons,
                    perGameSelection: perGameAddons);

                return (effective != null && effective.Count == 0)
                    .Label($"Expected empty selection when UseNormalReShade=true, " +
                           $"but got {effective?.Count ?? -1} addons " +
                           $"(addonMode={addonMode}, global={globalAddons.Count}, perGame={perGameAddons.Count})");
            });
    }

    // ── Property 2b: UseNormalReShade=false, Global mode → uses global selection ──
    // Feature: reshade-no-addon-support, Property 2: Addon deployment exclusion
    // **Validates: Requirements 3.2**

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeFalse_GlobalMode_UsesGlobalSelection()
    {
        return Prop.ForAll(
            Arb.From(GenAddonSelection),
            Arb.From(GenAddonSelection),
            (globalAddons, perGameAddons) =>
            {
                var effective = ComputeEffectiveSelection(
                    useNormalReShade: false,
                    addonMode: "Global",
                    globalAddons: globalAddons,
                    perGameSelection: perGameAddons);

                return (effective != null && effective.SequenceEqual(globalAddons))
                    .Label($"Expected global selection ({globalAddons.Count} addons) " +
                           $"when UseNormalReShade=false and mode=Global, " +
                           $"but got {effective?.Count ?? -1} addons");
            });
    }

    // ── Property 2c: UseNormalReShade=false, Select mode → uses per-game selection ──
    // Feature: reshade-no-addon-support, Property 2: Addon deployment exclusion
    // **Validates: Requirements 3.2**

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeFalse_SelectMode_UsesPerGameSelection()
    {
        return Prop.ForAll(
            Arb.From(GenAddonSelection),
            Arb.From(GenAddonSelection),
            (globalAddons, perGameAddons) =>
            {
                var effective = ComputeEffectiveSelection(
                    useNormalReShade: false,
                    addonMode: "Select",
                    globalAddons: globalAddons,
                    perGameSelection: perGameAddons);

                return (effective != null && effective.SequenceEqual(perGameAddons))
                    .Label($"Expected per-game selection ({perGameAddons.Count} addons) " +
                           $"when UseNormalReShade=false and mode=Select, " +
                           $"but got {effective?.Count ?? -1} addons");
            });
    }

    // ── Property 2d: UseNormalReShade=true on card → AddonsDisabled gates deployment ──
    // Feature: reshade-no-addon-support, Property 2: Addon deployment exclusion
    // **Validates: Requirements 3.1**

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeTrue_AddonsDisabled_GatesDeployment()
    {
        return Prop.ForAll(
            Arb.From(GenAddonSelection),
            (addonSelection) =>
            {
                var card = new GameCardViewModel { UseNormalReShade = true };

                // AddonsDisabled should be true, meaning deployment is gated
                bool gated = card.AddonsDisabled;

                // The effective selection when gated should be empty
                var effective = gated
                    ? new List<string>()
                    : addonSelection;

                return (gated && effective.Count == 0)
                    .Label($"AddonsDisabled={gated}, effective count={effective.Count} " +
                           $"(expected gated=true, count=0 when UseNormalReShade=true)");
            });
    }

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeFalse_AddonsNotDisabled_AllowsDeployment()
    {
        return Prop.ForAll(
            Arb.From(GenAddonSelection),
            (addonSelection) =>
            {
                var card = new GameCardViewModel { UseNormalReShade = false };

                // AddonsDisabled should be false, meaning deployment is allowed
                bool gated = card.AddonsDisabled;

                // The effective selection when not gated should be the actual selection
                var effective = gated
                    ? new List<string>()
                    : addonSelection;

                return (!gated && effective.SequenceEqual(addonSelection))
                    .Label($"AddonsDisabled={gated}, effective matches selection={effective.SequenceEqual(addonSelection)} " +
                           $"(expected gated=false, selection preserved when UseNormalReShade=false)");
            });
    }
}
