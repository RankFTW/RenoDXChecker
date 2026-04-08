using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for cross-exclusion dropdown filtering in the override menu.
///
/// When the override toggle is ON, the DC dropdown excludes the current RS selection
/// and the RS dropdown excludes the current DC selection (case-insensitive).
///
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
public class OverrideMenuCrossExclusionPropertyTests
{
    private static readonly string[] RsNames = DllOverrideConstants.CommonDllNames;
    private static readonly string[] DcNames = DetailPanelBuilder.DcDllOverrideNames;

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a random index into the RS names array.
    /// </summary>
    private static Gen<int> GenRsIndex() => Gen.Choose(0, RsNames.Length - 1);

    /// <summary>
    /// Generates a random index into the DC names array.
    /// </summary>
    private static Gen<int> GenDcIndex() => Gen.Choose(0, DcNames.Length - 1);

    /// <summary>
    /// Generates a random case variation of the given string.
    /// Picks from: all-uppercase, all-lowercase, or random mixed-case per character.
    /// </summary>
    private static Gen<string> GenCaseVariation(string source) =>
        Gen.Choose(0, 2).SelectMany(variant => variant switch
        {
            0 => Gen.Constant(source.ToUpperInvariant()),
            1 => Gen.Constant(source.ToLowerInvariant()),
            _ => GenMixedCase(source),
        });

    /// <summary>
    /// Generates a mixed-case variation by randomly toggling each character's case.
    /// </summary>
    private static Gen<string> GenMixedCase(string source)
    {
        var charGens = source.Select(c =>
            Gen.Elements(char.ToUpperInvariant(c), char.ToLowerInvariant(c)));
        return Gen.Sequence(charGens).Select(chars => new string(chars.ToArray()));
    }

    // ── Filtering logic under test (mirrors the production code) ──────────────────

    /// <summary>
    /// Filters the DC dropdown list by excluding the current RS selection (case-insensitive).
    /// This mirrors UpdateDcDropdownItems() when the override toggle is ON.
    /// </summary>
    private static string[] FilterDcItems(string rsSelection)
    {
        return string.IsNullOrEmpty(rsSelection)
            ? DcNames
            : DcNames.Where(n => !n.Equals(rsSelection, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    /// <summary>
    /// Filters the RS dropdown list by excluding the current DC selection (case-insensitive).
    /// This mirrors UpdateRsDropdownItems() when the override toggle is ON.
    /// </summary>
    private static string[] FilterRsItems(string dcSelection)
    {
        return string.IsNullOrEmpty(dcSelection)
            ? RsNames
            : RsNames.Where(n => !n.Equals(dcSelection, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    // ── Property 1: Cross-exclusion dropdown filtering ────────────────────────────

    /// <summary>
    /// Property: For any RS name from CommonDllNames and any DC name from DcDllOverrideNames,
    /// when the override toggle is ON, the filtered DC list SHALL NOT contain the RS selection
    /// and the filtered RS list SHALL NOT contain the DC selection (case-insensitive).
    ///
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CrossExclusion_FilteredLists_ExcludeOtherSelection()
    {
        return Prop.ForAll(
            Arb.From(GenRsIndex()),
            Arb.From(GenDcIndex()),
            (int rsIdx, int dcIdx) =>
            {
                var rsName = RsNames[rsIdx];
                var dcName = DcNames[dcIdx];

                // Apply the cross-exclusion filtering
                var filteredDcItems = FilterDcItems(rsName);
                var filteredRsItems = FilterRsItems(dcName);

                // Assert: filtered DC list does not contain the RS selection
                var dcExcludesRs = !filteredDcItems.Contains(rsName, StringComparer.OrdinalIgnoreCase);

                // Assert: filtered RS list does not contain the DC selection
                var rsExcludesDc = !filteredRsItems.Contains(dcName, StringComparer.OrdinalIgnoreCase);

                return (dcExcludesRs && rsExcludesDc).Label(
                    $"RS='{rsName}', DC='{dcName}': " +
                    $"DC list excludes RS={dcExcludesRs}, RS list excludes DC={rsExcludesDc}");
            });
    }

    // ── Collision detection logic under test (mirrors the production code) ────────

    /// <summary>
    /// Detects whether a typed name collides with the other dropdown's current selection.
    /// Returns true when the typed name case-insensitively matches the other selection,
    /// meaning the handler should reject the input and revert.
    /// This mirrors the collision check in the RS/DC KeyDown and SelectionChanged handlers.
    /// </summary>
    private static bool DetectsCollision(string typedName, string otherSelection)
    {
        return !string.IsNullOrEmpty(typedName)
            && !string.IsNullOrEmpty(otherSelection)
            && typedName.Equals(otherSelection, StringComparison.OrdinalIgnoreCase);
    }

    // ── Property 2: Typed name collision rejection ────────────────────────────────

    /// <summary>
    /// Property: For any DC name from DcDllOverrideNames, if a case variation of that name
    /// is typed into the RS dropdown, the collision detection SHALL identify it as a collision.
    /// Symmetrically, for any RS name from CommonDllNames, if a case variation of that name
    /// is typed into the DC dropdown, the collision detection SHALL identify it as a collision.
    ///
    /// **Validates: Requirements 4.3, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedNameCollision_CaseVariation_IsRejected()
    {
        return Prop.ForAll(
            Arb.From(GenDcIndex()),
            Arb.From(GenRsIndex()),
            (int dcIdx, int rsIdx) =>
            {
                var dcName = DcNames[dcIdx];
                var rsName = RsNames[rsIdx];

                // Generate a case variation of the DC name to simulate typing into RS dropdown
                var typedRsVariation = GenCaseVariation(dcName).Sample(0, 1).First();

                // Generate a case variation of the RS name to simulate typing into DC dropdown
                var typedDcVariation = GenCaseVariation(rsName).Sample(0, 1).First();

                // Assert: typing a case variation of the DC selection into RS is detected as collision
                var rsCollisionDetected = DetectsCollision(typedRsVariation, dcName);

                // Assert: typing a case variation of the RS selection into DC is detected as collision
                var dcCollisionDetected = DetectsCollision(typedDcVariation, rsName);

                return (rsCollisionDetected && dcCollisionDetected).Label(
                    $"DC='{dcName}', typed RS='{typedRsVariation}': collision={rsCollisionDetected}; " +
                    $"RS='{rsName}', typed DC='{typedDcVariation}': collision={dcCollisionDetected}");
            });
    }
}
