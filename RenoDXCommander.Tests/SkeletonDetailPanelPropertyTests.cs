// Feature: skeleton-loading-screen, Property 3: Skeleton detail panel contains all required sections
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for skeleton detail panel structural invariants.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
/// </summary>
public class SkeletonDetailPanelPropertyTests
{
    /// <summary>
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
    ///
    /// The skeleton detail panel spec always has:
    /// (a) header Height >= 16,
    /// (b) >= 3 badge placeholders,
    /// (c) component table with CornerRadius=12 and >= 2 rows of 5 columns,
    /// (d) Padding=(24,18,24,24) and Spacing=16.
    /// Tested across arbitrary invocations to confirm the spec is deterministic.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SkeletonDetailPanel_HasAllRequiredSections_ForAnyInvocation()
    {
        return Prop.ForAll(Arb.Default.Int32(), (_) =>
        {
            var spec = MainWindow.GetSkeletonDetailPanelSpec();

            // (a) Header height >= 16
            bool hasHeader = spec.HeaderHeight >= 16;

            // (b) >= 3 badge placeholders
            bool hasBadges = spec.BadgeCount >= 3;

            // (c) Component table: CornerRadius=12, >= 2 rows, 5 columns
            bool tableCornerRadius = spec.TableCornerRadius == 12;
            bool tableRows = spec.TableRowCount >= 2;
            bool tableColumns = spec.TableColumnCount == 5;

            // (d) Padding=(24,18,24,24) and Spacing=16
            bool padding = spec.PaddingLeft == 24 && spec.PaddingTop == 18
                        && spec.PaddingRight == 24 && spec.PaddingBottom == 24;
            bool spacing = spec.Spacing == 16;

            return (hasHeader && hasBadges && tableCornerRadius && tableRows && tableColumns && padding && spacing)
                .Label($"Header={hasHeader}, Badges={hasBadges}, TableCR={tableCornerRadius}, " +
                       $"TableRows={tableRows}, TableCols={tableColumns}, " +
                       $"Padding={padding}, Spacing={spacing}");
        });
    }
}
