using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for SanitizeDirectoryName.
/// Feature: screenshot-path-settings, Property 4: SanitizeDirectoryName removes exactly invalid characters
/// **Validates: Requirements 7.3**
/// </summary>
public class SanitizeDirectoryNamePropertyTests
{
    private static readonly char[] InvalidChars = { '<', '>', ':', '"', '/', '|', '?', '*' };

    // ── Property 4 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any input string, SanitizeDirectoryName should return a string that:
    /// 1. Contains no characters from the set &lt; &gt; : " / | ? *
    /// 2. Every character in the output appears in the input in the same relative order
    /// 3. Characters not in the invalid set are preserved exactly
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SanitizeDirectoryName_Removes_Invalid_And_Preserves_Valid()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            (string? input) =>
            {
                var normalizedInput = input ?? "";
                var result = AuxInstallService.SanitizeDirectoryName(normalizedInput);

                // 1. Output contains no invalid characters
                foreach (var c in result)
                {
                    if (InvalidChars.Contains(c))
                        return false.Label(
                            $"Output contains invalid char '{c}': input='{normalizedInput}', output='{result}'");
                }

                // 2. Every character in the output appears in the input in order
                //    (subsequence check)
                int inputIdx = 0;
                foreach (var c in result)
                {
                    while (inputIdx < normalizedInput.Length && normalizedInput[inputIdx] != c)
                        inputIdx++;

                    if (inputIdx >= normalizedInput.Length)
                        return false.Label(
                            $"Output char '{c}' not found in remaining input: input='{normalizedInput}', output='{result}'");

                    inputIdx++;
                }

                // 3. All valid characters from input are preserved (none dropped)
                var expectedValid = string.Concat(normalizedInput.Where(c => !InvalidChars.Contains(c)));
                if (result != expectedValid)
                    return false.Label(
                        $"Valid chars not preserved: expected='{expectedValid}', got='{result}'");

                return true.Label($"OK: input length={normalizedInput.Length}, output length={result.Length}");
            });
    }
}
