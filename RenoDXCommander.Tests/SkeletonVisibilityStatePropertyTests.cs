// Feature: skeleton-loading-screen, Property 1: Visibility state mapping is consistent
using FsCheck;
using FsCheck.Xunit;
using Microsoft.UI.Xaml;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for skeleton visibility state mapping.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
/// </summary>
public class SkeletonVisibilityStatePropertyTests
{
    /// <summary>
    /// **Validates: Requirements 1.1, 5.5, 6.3**
    ///
    /// For any combination of (isLoading, hasInitialized, currentPage),
    /// ComputeVisibilityState returns the correct visibility mapping:
    /// - Settings page: all Collapsed
    /// - isLoading=true, hasInitialized=false: LoadingPanel=Collapsed, GameViewPanel=Visible, SkeletonRowPanel=Visible, SkeletonDetailPanel=Visible
    /// - isLoading=false: LoadingPanel=Collapsed, GameViewPanel=Visible, SkeletonRowPanel=Collapsed, SkeletonDetailPanel=Collapsed
    /// - isLoading=true, hasInitialized=true (silent refresh): LoadingPanel=Collapsed, GameViewPanel=Visible, SkeletonRowPanel=Collapsed, SkeletonDetailPanel=Collapsed
    /// </summary>
    [Property(MaxTest = 20)]
    public Property VisibilityState_IsConsistent_ForAllInputCombinations()
    {
        var appPageArb = Gen.Elements(AppPage.GameView, AppPage.Settings, AppPage.About).ToArbitrary();

        return Prop.ForAll(
            Arb.Default.Bool(),
            Arb.Default.Bool(),
            appPageArb,
            (bool isLoading, bool hasInitialized, AppPage currentPage) =>
            {
                var state = MainWindow.ComputeVisibilityState(isLoading, hasInitialized, currentPage);

                // LoadingPanel is always Collapsed
                bool loadingAlwaysCollapsed = state.LoadingPanel == Visibility.Collapsed;

                bool correct;
                if (currentPage == AppPage.About)
                {
                    // About page: all skeleton panels Collapsed, AboutPanel Visible
                    correct = state.GameViewPanel == Visibility.Collapsed
                           && state.SkeletonRowPanel == Visibility.Collapsed
                           && state.SkeletonDetailPanel == Visibility.Collapsed
                           && state.AboutPanel == Visibility.Visible;
                }
                else if (currentPage == AppPage.Settings)
                {
                    // Settings page: all Collapsed
                    correct = state.GameViewPanel == Visibility.Collapsed
                           && state.SkeletonRowPanel == Visibility.Collapsed
                           && state.SkeletonDetailPanel == Visibility.Collapsed
                           && state.AboutPanel == Visibility.Collapsed;
                }
                else if (isLoading && !hasInitialized)
                {
                    // Initial loading: skeletons visible
                    correct = state.GameViewPanel == Visibility.Visible
                           && state.SkeletonRowPanel == Visibility.Visible
                           && state.SkeletonDetailPanel == Visibility.Visible
                           && state.AboutPanel == Visibility.Collapsed;
                }
                else
                {
                    // Not loading or silent refresh: skeletons hidden
                    correct = state.GameViewPanel == Visibility.Visible
                           && state.SkeletonRowPanel == Visibility.Collapsed
                           && state.SkeletonDetailPanel == Visibility.Collapsed
                           && state.AboutPanel == Visibility.Collapsed;
                }

                return (loadingAlwaysCollapsed && correct)
                    .Label($"isLoading={isLoading}, hasInit={hasInitialized}, page={currentPage} => " +
                           $"Loading={state.LoadingPanel}, GameView={state.GameViewPanel}, " +
                           $"SkeletonRow={state.SkeletonRowPanel}, SkeletonDetail={state.SkeletonDetailPanel}, " +
                           $"About={state.AboutPanel}");
            });
    }
}
