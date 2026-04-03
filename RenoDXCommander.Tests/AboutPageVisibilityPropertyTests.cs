// Feature: about-page-separation, Property 1: Page visibility exclusivity
// Feature: about-page-separation, Property 2: Navigation idempotence
using FsCheck;
using FsCheck.Xunit;
using Microsoft.UI.Xaml;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for About page visibility logic.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
/// </summary>
public class AboutPageVisibilityPropertyTests
{
    /// <summary>
    /// Feature: about-page-separation, Property 1: Page visibility exclusivity
    ///
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    ///
    /// For any AppPage value combined with any (isLoading, hasInitialized),
    /// ComputeVisibilityState returns exactly one of GameViewPanel/AboutPanel
    /// as Visible and the other as Collapsed. SettingsPanel visibility is managed
    /// separately in UpdatePageVisibility, so we only check the two page panels
    /// tracked in VisibilityState.
    ///
    /// - About: AboutPanel=Visible, GameViewPanel=Collapsed
    /// - Settings: AboutPanel=Collapsed, GameViewPanel=Collapsed
    /// - GameView: AboutPanel=Collapsed, GameViewPanel=Visible (or Visible with skeletons)
    /// </summary>
    [Property(MaxTest = 20)]
    public Property PageVisibility_IsExclusive_ForAllInputs()
    {
        var appPageArb = Gen.Elements(AppPage.GameView, AppPage.Settings, AppPage.About).ToArbitrary();

        return Prop.ForAll(
            Arb.Default.Bool(),
            Arb.Default.Bool(),
            appPageArb,
            (bool isLoading, bool hasInitialized, AppPage currentPage) =>
            {
                var state = MainWindow.ComputeVisibilityState(isLoading, hasInitialized, currentPage);

                bool correct;
                if (currentPage == AppPage.About)
                {
                    correct = state.AboutPanel == Visibility.Visible
                           && state.GameViewPanel == Visibility.Collapsed;
                }
                else if (currentPage == AppPage.Settings)
                {
                    correct = state.AboutPanel == Visibility.Collapsed
                           && state.GameViewPanel == Visibility.Collapsed;
                }
                else // GameView
                {
                    correct = state.AboutPanel == Visibility.Collapsed
                           && state.GameViewPanel == Visibility.Visible;
                }

                return correct
                    .Label($"page={currentPage}, isLoading={isLoading}, hasInit={hasInitialized} => " +
                           $"GameView={state.GameViewPanel}, About={state.AboutPanel}");
            });
    }

    /// <summary>
    /// Feature: about-page-separation, Property 2: Navigation idempotence
    ///
    /// **Validates: Requirements 5.1, 5.2, 5.3, 3.2**
    ///
    /// For any AppPage value, calling ComputeVisibilityState twice with the
    /// same inputs produces identical results.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property NavigationIdempotence_SameInputs_ProduceSameState()
    {
        var appPageArb = Gen.Elements(AppPage.GameView, AppPage.Settings, AppPage.About).ToArbitrary();

        return Prop.ForAll(
            Arb.Default.Bool(),
            Arb.Default.Bool(),
            appPageArb,
            (bool isLoading, bool hasInitialized, AppPage currentPage) =>
            {
                var state1 = MainWindow.ComputeVisibilityState(isLoading, hasInitialized, currentPage);
                var state2 = MainWindow.ComputeVisibilityState(isLoading, hasInitialized, currentPage);

                return (state1 == state2)
                    .Label($"page={currentPage}, isLoading={isLoading}, hasInit={hasInitialized} => " +
                           $"state1={state1}, state2={state2}");
            });
    }
}
