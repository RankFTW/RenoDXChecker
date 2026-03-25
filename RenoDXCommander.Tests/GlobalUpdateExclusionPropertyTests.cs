using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for global update exclusion toggle persistence.
/// Feature: override-menu-redesign, Property 6: Global update exclusion toggle persistence
/// </summary>
public class GlobalUpdateExclusionPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>Generates a non-empty, non-whitespace game name string.</summary>
    private static readonly Gen<string> GenGameName =
        Arb.Default.NonEmptyString().Generator
           .Select(nes => nes.Get)
           .Where(s => !string.IsNullOrWhiteSpace(s));

    /// <summary>Generates a random component index: 0=ReShade, 1=RenoDX, 2=ReLimiter.</summary>
    private static readonly Gen<int> GenComponent =
        Gen.Choose(0, 2);

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static bool GetExclusionState(MainViewModel vm, string gameName, int component) =>
        component switch
        {
            0 => vm.IsUpdateAllExcludedReShade(gameName),
            1 => vm.IsUpdateAllExcludedRenoDx(gameName),
            2 => vm.IsUpdateAllExcludedUl(gameName),
            _ => throw new ArgumentOutOfRangeException(nameof(component))
        };

    private static void ToggleExclusion(MainViewModel vm, string gameName, int component)
    {
        switch (component)
        {
            case 0: vm.ToggleUpdateAllExclusionReShade(gameName); break;
            case 1: vm.ToggleUpdateAllExclusionRenoDx(gameName); break;
            case 2: vm.ToggleUpdateAllExclusionUl(gameName); break;
            default: throw new ArgumentOutOfRangeException(nameof(component));
        }
    }

    private static string ComponentName(int component) =>
        component switch { 0 => "ReShade", 1 => "RenoDX", 2 => "ReLimiter", _ => "?" };

    // ── Property 6 ────────────────────────────────────────────────────────────────
    // **Validates: Requirements 4.7**

    [Property(MaxTest = 100)]
    public Property ToggleUpdateExclusion_FlipsOnlyChosenComponent()
    {
        return Prop.ForAll(
            Arb.From(GenGameName),
            Arb.From(GenComponent),
            (string gameName, int component) =>
            {
                var vm = TestHelpers.CreateMainViewModel();

                // Record initial exclusion states for all three components
                bool initialRs  = vm.IsUpdateAllExcludedReShade(gameName);
                bool initialRdx = vm.IsUpdateAllExcludedRenoDx(gameName);
                bool initialUl  = vm.IsUpdateAllExcludedUl(gameName);

                // Toggle the randomly chosen component
                ToggleExclusion(vm, gameName, component);

                // Read states after toggle
                bool afterRs  = vm.IsUpdateAllExcludedReShade(gameName);
                bool afterRdx = vm.IsUpdateAllExcludedRenoDx(gameName);
                bool afterUl  = vm.IsUpdateAllExcludedUl(gameName);

                // The chosen component must have flipped
                bool chosenFlipped = GetExclusionState(vm, gameName, component)
                                     != (component switch
                                     {
                                         0 => initialRs,
                                         1 => initialRdx,
                                         2 => initialUl,
                                         _ => false
                                     });

                // The other two must remain unchanged
                bool othersUnchanged = component switch
                {
                    0 => afterRdx == initialRdx && afterUl == initialUl,
                    1 => afterRs  == initialRs  && afterUl == initialUl,
                    2 => afterRs  == initialRs  && afterRdx == initialRdx,
                    _ => false
                };

                return (chosenFlipped && othersUnchanged)
                    .Label($"game='{gameName}', toggled={ComponentName(component)}, " +
                           $"RS:{initialRs}→{afterRs}, RDX:{initialRdx}→{afterRdx}, UL:{initialUl}→{afterUl}");
            });
    }
}
