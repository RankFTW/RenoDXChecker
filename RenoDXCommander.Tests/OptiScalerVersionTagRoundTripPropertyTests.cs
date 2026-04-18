using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for OptiScaler version tag round-trip via file I/O.
/// Feature: optiscaler-integration, Property 1: Version Tag Round-Trip
/// </summary>
public class OptiScalerVersionTagRoundTripPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates non-empty strings without path separator characters (/ and \).
    /// These represent valid version tag strings like "v0.8.1", "1.0.0-beta", etc.
    /// </summary>
    private static readonly Gen<string> GenVersionTag =
        from len in Gen.Choose(1, 40)
        from chars in Gen.ArrayOf(len, Gen.Elements(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-_+v".ToCharArray()))
        select new string(chars);

    // ── Property 1 ────────────────────────────────────────────────────────────────
    // Feature: optiscaler-integration, Property 1: Version Tag Round-Trip
    // **Validates: Requirements 1.3, 6.5**

    /// <summary>
    /// For any valid version tag string (non-empty, no path-separator characters),
    /// writing to version.txt and reading back produces the exact same string.
    /// This mirrors the OptiScalerService pattern: File.WriteAllText then
    /// File.ReadAllText().Trim().
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VersionTag_WriteAndReadBack_ProducesSameString()
    {
        return Prop.ForAll(GenVersionTag.ToArbitrary(), tag =>
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"rhi_test_version_{Guid.NewGuid():N}.txt");
            try
            {
                // Write — same as OptiScalerService.EnsureStagingAsync step 7
                File.WriteAllText(tempFile, tag);

                // Read — same as OptiScalerService.StagedVersion getter
                var readBack = File.ReadAllText(tempFile).Trim();

                return (readBack == tag)
                    .Label($"Written='{tag}', ReadBack='{readBack}'");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        });
    }
}
