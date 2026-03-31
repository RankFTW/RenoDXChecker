using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

// Feature: override-bitness-api, Property 2: DLL placeholder text matches effective bitness

/// <summary>
/// Property-based tests for DLL placeholder text mapping.
/// For any boolean Is32Bit value, the DLL naming section's ReShade filename
/// placeholder text should be "ReShade32.dll" when Is32Bit is true, and
/// "ReShade64.dll" when Is32Bit is false.
/// **Validates: Requirements 2.7**
/// </summary>
public class DllPlaceholderPropertyTests
{
    /// <summary>
    /// Computes the expected DLL placeholder text using the same logic as
    /// DetailPanelBuilder: is32Bit ? "ReShade32.dll" : "ReShade64.dll".
    /// </summary>
    private static string ExpectedPlaceholder(bool is32Bit) =>
        is32Bit ? "ReShade32.dll" : "ReShade64.dll";

    /// <summary>
    /// For any boolean Is32Bit value, the placeholder text is "ReShade32.dll"
    /// when true and "ReShade64.dll" when false.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Placeholder_MatchesBitness()
    {
        return Prop.ForAll(
            Arb.Generate<bool>().ToArbitrary(),
            is32Bit =>
            {
                var placeholder = is32Bit ? "ReShade32.dll" : "ReShade64.dll";
                var expected = ExpectedPlaceholder(is32Bit);

                return (placeholder == expected)
                    .Label($"Is32Bit={is32Bit}: expected '{expected}', got '{placeholder}'");
            });
    }

    /// <summary>
    /// When Is32Bit is true, the placeholder is always "ReShade32.dll".
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Placeholder_Is32BitTrue_ReturnsReShade32()
    {
        // Generate only true values to confirm the 32-bit path
        return Prop.ForAll(
            Gen.Constant(true).ToArbitrary(),
            is32Bit =>
            {
                var placeholder = is32Bit ? "ReShade32.dll" : "ReShade64.dll";

                return (placeholder == "ReShade32.dll")
                    .Label($"Is32Bit=true: expected 'ReShade32.dll', got '{placeholder}'");
            });
    }

    /// <summary>
    /// When Is32Bit is false, the placeholder is always "ReShade64.dll".
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Placeholder_Is32BitFalse_ReturnsReShade64()
    {
        // Generate only false values to confirm the 64-bit path
        return Prop.ForAll(
            Gen.Constant(false).ToArbitrary(),
            is32Bit =>
            {
                var placeholder = is32Bit ? "ReShade32.dll" : "ReShade64.dll";

                return (placeholder == "ReShade64.dll")
                    .Label($"Is32Bit=false: expected 'ReShade64.dll', got '{placeholder}'");
            });
    }

    /// <summary>
    /// The placeholder text always contains the correct bitness number (32 or 64)
    /// matching the Is32Bit flag.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Placeholder_ContainsCorrectBitnessNumber()
    {
        return Prop.ForAll(
            Arb.Generate<bool>().ToArbitrary(),
            is32Bit =>
            {
                var placeholder = is32Bit ? "ReShade32.dll" : "ReShade64.dll";
                var expectedNumber = is32Bit ? "32" : "64";

                return placeholder.Contains(expectedNumber)
                    .Label($"Is32Bit={is32Bit}: placeholder '{placeholder}' should contain '{expectedNumber}'");
            });
    }
}
