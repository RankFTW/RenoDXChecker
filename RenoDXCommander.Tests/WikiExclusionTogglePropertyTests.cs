using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for wiki exclusion toggle idempotent-flip behaviour.
/// </summary>
public class WikiExclusionTogglePropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>Generates a non-empty, non-whitespace game name string.</summary>
    private static readonly Gen<string> GenGameName =
        Arb.Default.NonEmptyString().Generator
           .Select(nes => nes.Get)
           .Where(s => !string.IsNullOrWhiteSpace(s));

    // ── Property 3 ────────────────────────────────────────────────────────────────
    // Feature: override-menu-redesign, Property 3: Wiki exclusion toggle is idempotent-flip
    // **Validates: Requirements 4.3**

    [Property(MaxTest = 100)]
    public Property ToggleWikiExclusion_Twice_ReturnsToOriginalState()
    {
        return Prop.ForAll(
            Arb.From(GenGameName),
            (string gameName) =>
            {
                var vm = TestHelpers.CreateMainViewModel();

                // Record initial exclusion state
                bool initialState = vm.IsWikiExcluded(gameName);

                // Toggle once — state should flip
                vm.ToggleWikiExclusion(gameName);
                bool afterFirstToggle = vm.IsWikiExcluded(gameName);

                // Toggle again — state should return to original
                vm.ToggleWikiExclusion(gameName);
                bool afterSecondToggle = vm.IsWikiExcluded(gameName);

                bool flipped = afterFirstToggle != initialState;
                bool restored = afterSecondToggle == initialState;

                return (flipped && restored)
                    .Label($"gameName='{gameName}', initial={initialState}, afterFirst={afterFirstToggle}, afterSecond={afterSecondToggle}");
            });
    }
}
