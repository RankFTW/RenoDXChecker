using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for OptiScaler LoadReshade=true enforcement.
/// Feature: optiscaler-integration, Property 6: LoadReshade=true Enforcement
/// </summary>
public class OptiScalerLoadReshadeEnforcementPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a random INI-style line that is NOT a LoadReshade line.
    /// Produces lines like "[Section]", "SomeKey=SomeValue", comments, or blank lines.
    /// </summary>
    private static readonly Gen<string> GenNonLoadReshadeLine =
        Gen.OneOf(
            // Section headers
            from name in Gen.Elements("OptiScaler", "Hotkey", "Display", "General", "Advanced")
            select $"[{name}]",
            // Key=Value pairs (keys that are not LoadReshade)
            from key in Gen.Elements("ShortcutKey", "UpscaleMethod", "OutputScaling", "Sharpness", "DlssQuality")
            from val in Gen.Elements("true", "false", "1", "0", "Insert", "Home", "fsr", "xess")
            select $"{key}={val}",
            // Comment lines
            from text in Gen.Elements("this is a comment", "configuration", "user settings")
            select $"; {text}",
            // Blank lines
            Gen.Constant("")
        );

    /// <summary>
    /// Generates a LoadReshade= line with a random value and optional leading whitespace.
    /// Covers variations like "LoadReshade=false", "  loadreshade=TRUE", "LOADRESHADE=0", etc.
    /// </summary>
    private static readonly Gen<string> GenLoadReshadeLine =
        from leadingSpaces in Gen.Choose(0, 4)
        from casing in Gen.Elements("LoadReshade", "loadreshade", "LOADRESHADE", "loadReshade", "Loadreshade")
        from value in Gen.Elements("true", "false", "TRUE", "FALSE", "0", "1", "yes", "no", "")
        select new string(' ', leadingSpaces) + $"{casing}={value}";

    /// <summary>
    /// Generates random INI content as a list of lines. Some lines may be LoadReshade= lines,
    /// most will be other INI content. This covers:
    /// - Files with no LoadReshade line
    /// - Files with exactly one LoadReshade line (any value)
    /// - Files with multiple LoadReshade lines
    /// </summary>
    private static readonly Gen<string[]> GenIniContent =
        from lineCount in Gen.Choose(0, 20)
        from lines in Gen.ArrayOf(lineCount, Gen.Frequency(
            Tuple.Create(5, GenNonLoadReshadeLine),
            Tuple.Create(1, GenLoadReshadeLine)))
        select lines;

    // ── Property 6 ────────────────────────────────────────────────────────────────
    // Feature: optiscaler-integration, Property 6: LoadReshade=true Enforcement
    // **Validates: Requirements 3.7, 3.10, 18.2**

    /// <summary>
    /// For any OptiScaler.ini content (with or without an existing LoadReshade line,
    /// with any value), after EnforceLoadReshade the result contains exactly one line
    /// matching LoadReshade=true and no other LoadReshade= lines exist.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property EnforceLoadReshade_ResultContainsExactlyOneLoadReshadeTrue()
    {
        return Prop.ForAll(GenIniContent.ToArbitrary(), iniLines =>
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"rhi_test_ini_{Guid.NewGuid():N}.ini");
            try
            {
                // Arrange — write generated INI content to temp file
                File.WriteAllLines(tempFile, iniLines);

                // Act — call the method under test
                OptiScalerService.EnforceLoadReshade(tempFile);

                // Assert — read back and verify
                var resultLines = File.ReadAllLines(tempFile);

                // Count lines that are exactly "LoadReshade=true" (the enforced value)
                var loadReshadeTrue = resultLines
                    .Count(l => l.TrimStart().Equals("LoadReshade=true", StringComparison.Ordinal));

                // Count all lines starting with "LoadReshade=" (case-insensitive)
                var allLoadReshade = resultLines
                    .Count(l => l.TrimStart().StartsWith("LoadReshade=", StringComparison.OrdinalIgnoreCase));

                return (loadReshadeTrue == 1 && allLoadReshade == 1)
                    .Label($"Expected exactly 1 LoadReshade=true and no others. " +
                           $"Found {loadReshadeTrue} 'LoadReshade=true' and {allLoadReshade} total 'LoadReshade=' lines. " +
                           $"Input had {iniLines.Length} lines.");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        });
    }
}
