// Feature: skeleton-loading-screen, Property 5: Skeleton cleanup is complete
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for skeleton cleanup completeness.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
/// </summary>
public class SkeletonCleanupPropertyTests
{
    /// <summary>
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    ///
    /// For any initial state where SkeletonRowPanel has N > 0 children and
    /// SkeletonDetailPanel has M > 0 children and storyboard is non-null,
    /// ComputeCleanupResult returns (ClearChildren=true, Collapse=true, NullStoryboard=true).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RemoveSkeletons_CleansUpCompletely_ForAnyChildCounts()
    {
        var countArb = Gen.Choose(1, 30).ToArbitrary();

        return Prop.ForAll(countArb, countArb, (int rowCount, int detailCount) =>
        {
            var (clearChildren, collapse, nullStoryboard) =
                MainWindow.ComputeCleanupResult(rowCount, detailCount, hasStoryboard: true);

            return (clearChildren && collapse && nullStoryboard)
                .Label($"N={rowCount}, M={detailCount}: Clear={clearChildren}, " +
                       $"Collapse={collapse}, NullStoryboard={nullStoryboard}");
        });
    }

    /// <summary>
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    ///
    /// Cleanup is idempotent: calling ComputeCleanupResult with already-cleaned state
    /// (0 children, no storyboard) still returns (ClearChildren=true, Collapse=true, NullStoryboard=false).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RemoveSkeletons_IsIdempotent_ForAlreadyCleanedState()
    {
        return Prop.ForAll(Arb.Default.Int32(), (_) =>
        {
            // Already cleaned: 0 children, no storyboard
            var (clearChildren, collapse, nullStoryboard) =
                MainWindow.ComputeCleanupResult(0, 0, hasStoryboard: false);

            // Should still indicate clear and collapse (idempotent), but no storyboard to null
            return (clearChildren && collapse && !nullStoryboard)
                .Label($"Clear={clearChildren}, Collapse={collapse}, NullStoryboard={nullStoryboard}");
        });
    }
}
