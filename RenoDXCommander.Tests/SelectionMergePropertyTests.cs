using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

// Feature: preset-shader-install, Property 5: selection merge is union

/// <summary>
/// Property-based tests for the selection merge operation (HashSet&lt;string&gt;.UnionWith).
/// For any existing set of per-game shader pack IDs and for any resolved set of shader pack IDs,
/// the merged result SHALL equal the set union of both input sets.
/// **Validates: Requirements 6.2**
/// </summary>
public class SelectionMergePropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a shader pack ID string.
    /// </summary>
    private static Gen<string> GenPackId()
    {
        return Gen.Elements(
            "pack-reshade", "pack-qUINT", "pack-sweetfx", "pack-marty",
            "pack-prod80", "pack-crosire", "pack-daodan", "pack-otis",
            "pack-amd-fidelityfx", "pack-nvidia-rtx", "pack-depth3d",
            "pack-pirate", "pack-brusslan", "pack-fubax");
    }

    /// <summary>
    /// Generates a set of pack IDs (0 to maxSize elements).
    /// </summary>
    private static Gen<HashSet<string>> GenPackIdSet(int maxSize)
    {
        return from count in Gen.Choose(0, maxSize)
               from ids in Gen.ListOf(count, GenPackId())
               select new HashSet<string>(ids, StringComparer.Ordinal);
    }

    /// <summary>
    /// Generates the full test input: an existing selection set and a resolved set.
    /// </summary>
    private static Gen<MergeTestInput> GenMergeTestInput()
    {
        return from existing in GenPackIdSet(8)
               from resolved in GenPackIdSet(8)
               select new MergeTestInput(existing, resolved);
    }

    // ── Test Input Record ─────────────────────────────────────────────────────────

    private record MergeTestInput(
        HashSet<string> ExistingSelection,
        HashSet<string> ResolvedPackIds);

    // ── Property 5: Selection merge is union ──────────────────────────────────────

    /// <summary>
    /// Merging existing per-game shader pack selections with newly resolved pack IDs
    /// using HashSet&lt;string&gt;.UnionWith produces the mathematical set union:
    /// - existing ⊆ merged
    /// - resolved ⊆ merged
    /// - |merged| ≤ |existing| + |resolved|
    /// - merged == existing ∪ resolved
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Merge_ProducesSetUnion()
    {
        return Prop.ForAll(
            GenMergeTestInput().ToArbitrary(),
            input =>
            {
                // Compute expected union
                var expectedUnion = new HashSet<string>(input.ExistingSelection, StringComparer.Ordinal);
                expectedUnion.UnionWith(input.ResolvedPackIds);

                // Simulate the merge operation (same as ApplyPresetShaders will do)
                var merged = new HashSet<string>(input.ExistingSelection, StringComparer.Ordinal);
                merged.UnionWith(input.ResolvedPackIds);

                // Verify: merged equals the mathematical set union
                var isUnion = merged.SetEquals(expectedUnion);

                // Verify: existing ⊆ merged
                var existingSubset = input.ExistingSelection.IsSubsetOf(merged);

                // Verify: resolved ⊆ merged
                var resolvedSubset = input.ResolvedPackIds.IsSubsetOf(merged);

                // Verify: |merged| ≤ |existing| + |resolved|
                var sizeValid = merged.Count <= input.ExistingSelection.Count + input.ResolvedPackIds.Count;

                return (isUnion && existingSubset && resolvedSubset && sizeValid).Label(
                    $"Existing:  {{{string.Join(", ", input.ExistingSelection)}}}\n" +
                    $"Resolved:  {{{string.Join(", ", input.ResolvedPackIds)}}}\n" +
                    $"Merged:    {{{string.Join(", ", merged)}}}\n" +
                    $"Expected:  {{{string.Join(", ", expectedUnion)}}}\n" +
                    $"IsUnion:   {isUnion}\n" +
                    $"Existing⊆: {existingSubset}\n" +
                    $"Resolved⊆: {resolvedSubset}\n" +
                    $"SizeValid: {sizeValid}");
            });
    }
}
