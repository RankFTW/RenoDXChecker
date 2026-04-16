using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for OptiScaler hotkey INI round-trip.
/// Feature: optiscaler-integration, Property 13: Hotkey INI Round-Trip
/// **Validates: Requirements 11.3**
/// </summary>
public class OptiScalerHotkeyRoundTripPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a non-empty string of printable ASCII characters (0x20–0x7E)
    /// suitable for use as a hotkey value (e.g. "Insert", "F12", "Home").
    /// </summary>
    private static readonly Gen<string> GenPrintableAsciiHotkey =
        from length in Gen.Choose(1, 30)
        from chars in Gen.ArrayOf(length, Gen.Choose(0x20, 0x7E).Select(c => (char)c))
        select new string(chars);

    /// <summary>
    /// Generates random INI-style lines that are NOT ShortcutKey lines.
    /// Produces lines like "[Section]", "SomeKey=SomeValue", comments, or blank lines.
    /// </summary>
    private static readonly Gen<string> GenNonShortcutKeyLine =
        Gen.OneOf(
            // Section headers
            from name in Gen.Elements("OptiScaler", "Hotkey", "Display", "General", "Advanced")
            select $"[{name}]",
            // Key=Value pairs (keys that are not ShortcutKey)
            from key in Gen.Elements("LoadReshade", "UpscaleMethod", "OutputScaling", "Sharpness", "DlssQuality")
            from val in Gen.Elements("true", "false", "1", "0", "fsr", "xess")
            select $"{key}={val}",
            // Comment lines
            from text in Gen.Elements("this is a comment", "configuration", "user settings")
            select $"; {text}",
            // Blank lines
            Gen.Constant("")
        );

    /// <summary>
    /// Generates random INI content as a list of lines. Some lines may be ShortcutKey= lines
    /// with random values, most will be other INI content.
    /// </summary>
    private static readonly Gen<string[]> GenIniContent =
        from lineCount in Gen.Choose(0, 15)
        from lines in Gen.ArrayOf(lineCount, Gen.Frequency(
            Tuple.Create(5, GenNonShortcutKeyLine),
            Tuple.Create(1,
                from val in Gen.Elements("Insert", "Home", "End", "F1", "Delete")
                select $"ShortcutKey={val}")))
        select lines;

    // ── Property 13 ───────────────────────────────────────────────────────────────
    // Feature: optiscaler-integration, Property 13: Hotkey INI Round-Trip
    // **Validates: Requirements 11.3**

    /// <summary>
    /// For any valid hotkey string (non-empty, printable ASCII), writing ShortcutKey=&lt;value&gt;
    /// to an INI file and reading back produces the same value.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property WriteShortcutKey_ThenRead_RoundTrips()
    {
        return Prop.ForAll(
            GenIniContent.ToArbitrary(),
            GenPrintableAsciiHotkey.ToArbitrary(),
            (string[] iniLines, string hotkeyValue) =>
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"rhi_test_os_hotkey_{Guid.NewGuid():N}.ini");
                try
                {
                    // Arrange — write generated INI content to temp file
                    File.WriteAllLines(tempFile, iniLines);

                    // Act — write the hotkey value
                    OptiScalerService.WriteShortcutKey(tempFile, hotkeyValue);

                    // Assert — read back and verify
                    var readBack = OptiScalerService.ReadShortcutKey(tempFile);

                    if (readBack == null)
                        return false.Label($"ReadShortcutKey returned null after writing '{hotkeyValue}'");

                    return (readBack == hotkeyValue)
                        .Label($"Round-trip mismatch: wrote='{hotkeyValue}', read='{readBack}'. " +
                               $"Input had {iniLines.Length} lines.");
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            });
    }

    /// <summary>
    /// For any valid hotkey string, after WriteShortcutKey there is exactly one
    /// ShortcutKey= line in the file, regardless of how many existed before.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property WriteShortcutKey_ProducesExactlyOneLine()
    {
        return Prop.ForAll(
            GenIniContent.ToArbitrary(),
            GenPrintableAsciiHotkey.ToArbitrary(),
            (string[] iniLines, string hotkeyValue) =>
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"rhi_test_os_hotkey_count_{Guid.NewGuid():N}.ini");
                try
                {
                    File.WriteAllLines(tempFile, iniLines);

                    OptiScalerService.WriteShortcutKey(tempFile, hotkeyValue);

                    var resultLines = File.ReadAllLines(tempFile);
                    var shortcutKeyCount = resultLines
                        .Count(l => l.TrimStart().StartsWith("ShortcutKey=", StringComparison.OrdinalIgnoreCase));

                    return (shortcutKeyCount == 1)
                        .Label($"Expected exactly 1 ShortcutKey= line, found {shortcutKeyCount}. " +
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
