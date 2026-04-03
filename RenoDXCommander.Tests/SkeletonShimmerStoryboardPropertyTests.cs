// Feature: skeleton-loading-screen, Property 4: Shimmer storyboard is correctly configured per target
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for shimmer storyboard configuration.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
/// </summary>
public class SkeletonShimmerStoryboardPropertyTests
{
    /// <summary>
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    ///
    /// For any target count (1–50), GetShimmerAnimationSpecs produces exactly
    /// one spec per target, each with AutoReverse=true, RepeatForever=true,
    /// and Duration between 1.0–2.0s.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ShimmerStoryboard_HasCorrectAnimations_ForAnyTargetCount()
    {
        var countArb = Gen.Choose(1, 50).ToArbitrary();

        return Prop.ForAll(countArb, (int count) =>
        {
            var specs = MainWindow.GetShimmerAnimationSpecs(count);

            // Animation count equals target count
            bool correctCount = specs.Count == count;

            // Each animation has correct properties
            bool allCorrect = true;
            foreach (var spec in specs)
            {
                if (!spec.AutoReverse || !spec.RepeatForever
                    || spec.DurationSeconds < 1.0 || spec.DurationSeconds > 2.0)
                {
                    allCorrect = false;
                    break;
                }
            }

            return (correctCount && allCorrect)
                .Label($"Count={count}: SpecCount={specs.Count}, AllCorrect={allCorrect}");
        });
    }
}
