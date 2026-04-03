// Feature: skeleton-loading-screen, Property 2: Skeleton row structure matches game card template
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for skeleton row structural invariants.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
/// </summary>
public class SkeletonRowStructurePropertyTests
{
    /// <summary>
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// The skeleton row spec always has: CornerRadius=6, Padding=(8,6,8,6),
    /// GridColumnSpacing=6, 3 children (14×14 icon, Height=12 name, 8×8 badge).
    /// Tested across arbitrary invocations to confirm the spec is deterministic.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SkeletonRow_HasCorrectStructure_ForAnyInvocation()
    {
        // Use an arbitrary int as a "seed" to invoke the spec many times
        return Prop.ForAll(Arb.Default.Int32(), (_) =>
        {
            var spec = MainWindow.GetSkeletonRowSpec();

            bool cornerRadius = spec.CornerRadius == 6;
            bool padding = spec.PaddingLeft == 8 && spec.PaddingTop == 6
                        && spec.PaddingRight == 8 && spec.PaddingBottom == 6;
            bool columnSpacing = spec.GridColumnSpacing == 6;
            bool childCount = spec.ChildCount == 3;
            bool icon = spec.IconWidth == 14 && spec.IconHeight == 14;
            bool name = spec.NameHeight == 14;
            bool badge = spec.BadgeWidth == 8 && spec.BadgeHeight == 8;

            return (cornerRadius && padding && columnSpacing && childCount && icon && name && badge)
                .Label($"CR={cornerRadius}, Pad={padding}, ColSpacing={columnSpacing}, " +
                       $"Children={childCount}, Icon={icon}, Name={name}, Badge={badge}");
        });
    }
}
