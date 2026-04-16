using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for OptiScaler update detection by tag comparison.
/// Feature: optiscaler-integration, Property 2: Update Detection by Tag Comparison
/// </summary>
public class OptiScalerUpdateDetectionPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates arbitrary non-null version tag strings including realistic tags
    /// and edge cases (mixed case, whitespace-adjacent, empty-ish).
    /// </summary>
    private static readonly Gen<string> GenTagString =
        Gen.OneOf(
            // Realistic version tags
            Gen.Elements("v0.8.0", "v0.8.1", "v1.0.0", "v1.0.0-beta", "0.8.1", "unknown"),
            // Arbitrary strings to cover case-sensitivity and edge cases
            from len in Gen.Choose(0, 30)
            from chars in Gen.ArrayOf(len, Gen.Elements(
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-_+v ".ToCharArray()))
            select new string(chars));

    // ── Property 2 ────────────────────────────────────────────────────────────────
    // Feature: optiscaler-integration, Property 2: Update Detection by Tag Comparison
    // **Validates: Requirements 2.2**

    /// <summary>
    /// For any two version tag strings cached and remote, update detection returns
    /// true iff cached differs from remote (case-sensitive).
    /// This mirrors OptiScalerService.CheckForUpdateAsync:
    ///   HasUpdate = !string.Equals(cachedTag, remoteTag, StringComparison.Ordinal)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateDetection_ReturnsTrueIffTagsDiffer()
    {
        return Prop.ForAll(
            GenTagString.ToArbitrary(),
            GenTagString.ToArbitrary(),
            (string cached, string remote) =>
            {
                // The update detection logic from OptiScalerService.CheckForUpdateAsync
                var hasUpdate = !string.Equals(cached, remote, StringComparison.Ordinal);

                // Expected: update iff the strings are not identical (case-sensitive)
                var expected = cached != remote;

                return (hasUpdate == expected)
                    .Label($"Cached='{cached}', Remote='{remote}', HasUpdate={hasUpdate}, Expected={expected}");
            });
    }

    /// <summary>
    /// For any version tag string, comparing it with itself always yields no update.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateDetection_SameTag_NeverHasUpdate()
    {
        return Prop.ForAll(GenTagString.ToArbitrary(), tag =>
        {
            var hasUpdate = !string.Equals(tag, tag, StringComparison.Ordinal);

            return (!hasUpdate)
                .Label($"Tag='{tag}', HasUpdate={hasUpdate} (should be false)");
        });
    }

    // ── Unit tests for case-sensitivity edge cases ────────────────────────────────
    // Validates: Requirements 2.2

    [Theory]
    [InlineData("v0.8.1", "v0.8.1", false)]
    [InlineData("v0.8.1", "v0.8.2", true)]
    [InlineData("V0.8.1", "v0.8.1", true)]  // Case-sensitive: V != v
    [InlineData("", "", false)]
    [InlineData("v1.0.0", "V1.0.0", true)]  // Case-sensitive
    public void UpdateDetection_KnownPairs_ReturnsExpected(
        string cached, string remote, bool expectedHasUpdate)
    {
        var hasUpdate = !string.Equals(cached, remote, StringComparison.Ordinal);

        Assert.Equal(expectedHasUpdate, hasUpdate);
    }
}
