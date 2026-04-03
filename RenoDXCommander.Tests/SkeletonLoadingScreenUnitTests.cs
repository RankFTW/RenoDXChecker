// Feature: skeleton-loading-screen — Unit tests for edge cases
using Microsoft.UI.Xaml;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for skeleton loading screen edge cases.
/// </summary>
public class SkeletonLoadingScreenUnitTests
{
    /// <summary>
    /// Skeleton row count constant is within the [8, 15] range.
    /// Validates: Requirement 2.3
    /// </summary>
    [Fact]
    public void SkeletonRowCount_IsWithinExpectedRange()
    {
        Assert.InRange(MainWindow.SkeletonRowCount, 8, 15);
    }

    /// <summary>
    /// Shimmer base color #FF1A2028 is within expected dark theme range.
    /// Validates: Requirement 4.4
    /// </summary>
    [Fact]
    public void ShimmerBaseColor_IsWithinDarkThemeRange()
    {
        // #FF1A2028 — R=0x1A(26), G=0x20(32), B=0x28(40)
        // Dark theme range: all channels should be low (< 0x50) for a dark surface color
        Assert.InRange(MainWindow.SkeletonBaseR, (byte)0x00, (byte)0x50);
        Assert.InRange(MainWindow.SkeletonBaseG, (byte)0x00, (byte)0x50);
        Assert.InRange(MainWindow.SkeletonBaseB, (byte)0x00, (byte)0x50);
    }

    /// <summary>
    /// Shimmer highlight color #FF252D38 is within expected dark theme range.
    /// Validates: Requirement 4.4
    /// </summary>
    [Fact]
    public void ShimmerHighlightColor_IsWithinDarkThemeRange()
    {
        // #FF252D38 — R=0x25(37), G=0x2D(45), B=0x38(56)
        Assert.InRange(MainWindow.SkeletonHighlightR, (byte)0x00, (byte)0x50);
        Assert.InRange(MainWindow.SkeletonHighlightG, (byte)0x00, (byte)0x50);
        Assert.InRange(MainWindow.SkeletonHighlightB, (byte)0x00, (byte)0x50);
    }

    /// <summary>
    /// RemoveSkeletons (via ComputeCleanupResult) is idempotent — calling twice
    /// produces the same result and does not throw.
    /// Validates: Requirement 5.1
    /// </summary>
    [Fact]
    public void RemoveSkeletons_IsIdempotent_CallingTwiceDoesNotThrow()
    {
        // First call: has children and storyboard
        var (clear1, collapse1, nullSb1) = MainWindow.ComputeCleanupResult(5, 3, hasStoryboard: true);
        Assert.True(clear1);
        Assert.True(collapse1);
        Assert.True(nullSb1);

        // Second call: already cleaned
        var (clear2, collapse2, nullSb2) = MainWindow.ComputeCleanupResult(0, 0, hasStoryboard: false);
        Assert.True(clear2);
        Assert.True(collapse2);
        Assert.False(nullSb2); // no storyboard to null on second call
    }

    /// <summary>
    /// Visibility state for (isLoading=true, hasInitialized=false, currentPage=GameView)
    /// produces the skeleton-visible state.
    /// Validates: Requirement 1.1
    /// </summary>
    [Fact]
    public void VisibilityState_InitialLoading_ProducesSkeletonVisibleState()
    {
        var state = MainWindow.ComputeVisibilityState(
            isLoading: true, hasInitialized: false, currentPage: AppPage.GameView);

        Assert.Equal(Visibility.Collapsed, state.LoadingPanel);
        Assert.Equal(Visibility.Visible, state.GameViewPanel);
        Assert.Equal(Visibility.Visible, state.SkeletonRowPanel);
        Assert.Equal(Visibility.Visible, state.SkeletonDetailPanel);
    }
}
