using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for INI section preservation after screenshot path apply.
/// Feature: screenshot-path-settings, Property 5: Other INI sections preserved
/// **Validates: Requirements 4.6, 6.4**
/// </summary>
public class ScreenshotIniPreservationPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenSectionName =
        from len in Gen.Choose(1, 10)
        from chars in Gen.ListOf(len, Gen.Elements(
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
        select new string(chars.ToArray());

    /// <summary>
    /// Generates section names that are NOT "SCREENSHOT" (case-insensitive).
    /// </summary>
    private static readonly Gen<string> GenNonScreenshotSectionName =
        GenSectionName.Where(s => !s.Equals("SCREENSHOT", StringComparison.OrdinalIgnoreCase));

    private static readonly Gen<string> GenKeyName =
        from len in Gen.Choose(1, 8)
        from chars in Gen.ListOf(len, Gen.Elements(
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z'))
        select new string(chars.ToArray());

    /// <summary>
    /// Generates key names that are NOT "SavePath" (case-insensitive).
    /// </summary>
    private static readonly Gen<string> GenNonSavePathKey =
        GenKeyName.Where(k => !k.Equals("SavePath", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Generates a value that won't be altered by INI round-trip (no trailing whitespace,
    /// since ParseIni calls TrimEnd on lines).
    /// </summary>
    private static readonly Gen<string> GenValue =
        from len in Gen.Choose(0, 15)
        from chars in Gen.ListOf(len, Gen.Choose(32, 126).Select(i => (char)i))
        select new string(chars.ToArray()).TrimEnd();

    private static readonly Gen<string> GenScreenshotPath =
        Gen.Elements(
            @"D:\Screenshots", @"C:\Users\Test\Pictures",
            @"E:\Games\Screenshots", @"C:\temp");

    /// <summary>
    /// Generates INI content with 1-3 non-SCREENSHOT sections, each with 1-3 keys,
    /// plus optionally a SCREENSHOT section with extra keys (not SavePath).
    /// </summary>
    private static Gen<(string[] lines, Dictionary<string, Dictionary<string, string>> expectedSections)> GenIniWithSections()
    {
        return from sectionCount in Gen.Choose(1, 3)
               from sectionNames in Gen.ListOf(sectionCount, GenNonScreenshotSectionName)
               from includeScreenshot in Arb.Default.Bool().Generator
               let uniqueNames = sectionNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
               from sections in Gen.Sequence(uniqueNames.Select(name =>
                   from keyCount in Gen.Choose(1, 3)
                   from keys in Gen.ListOf(keyCount, GenKeyName)
                   from values in Gen.ListOf(keyCount, GenValue)
                   let pairs = keys.Zip(values)
                       .GroupBy(kv => kv.First, StringComparer.OrdinalIgnoreCase)
                       .ToDictionary(g => g.Key, g => g.First().Second, StringComparer.OrdinalIgnoreCase)
                   select (name, pairs)))
               from screenshotExtraKeys in Gen.Choose(0, includeScreenshot ? 2 : 0)
               from extraKeys in Gen.ListOf(screenshotExtraKeys, GenNonSavePathKey)
               from extraValues in Gen.ListOf(screenshotExtraKeys, GenValue)
               let screenshotExtras = extraKeys.Zip(extraValues)
                   .GroupBy(kv => kv.First, StringComparer.OrdinalIgnoreCase)
                   .ToDictionary(g => g.Key, g => g.First().Second, StringComparer.OrdinalIgnoreCase)
               select BuildIniData(sections.ToList(), includeScreenshot, screenshotExtras);
    }

    private static (string[] lines, Dictionary<string, Dictionary<string, string>> expectedSections) BuildIniData(
        List<(string name, Dictionary<string, string> pairs)> sections,
        bool includeScreenshot,
        Dictionary<string, string> screenshotExtras)
    {
        var lines = new List<string>();
        var expected = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, pairs) in sections)
        {
            lines.Add($"[{name}]");
            expected[name] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in pairs)
            {
                lines.Add($"{key}={value}");
                expected[name][key] = value;
            }
            lines.Add("");
        }

        if (includeScreenshot && screenshotExtras.Count > 0)
        {
            lines.Add("[SCREENSHOT]");
            expected["SCREENSHOT"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in screenshotExtras)
            {
                lines.Add($"{key}={value}");
                expected["SCREENSHOT"][key] = value;
            }
            lines.Add("");
        }

        return (lines.ToArray(), expected);
    }

    // ── Property 5 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any valid INI file content with arbitrary sections and keys, after calling
    /// ApplyScreenshotPath, all sections other than [SCREENSHOT] should have identical
    /// keys and values as before the call. Keys within [SCREENSHOT] other than SavePath
    /// should also be preserved.
    /// **Validates: Requirements 4.6, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ApplyScreenshotPath_Preserves_Other_Sections_And_Keys()
    {
        return Prop.ForAll(
            Arb.From(GenIniWithSections()),
            Arb.From(GenScreenshotPath),
            ((string[] lines, Dictionary<string, Dictionary<string, string>> expectedSections) iniData, string savePath) =>
            {
                var tempFile = Path.GetTempFileName();
                try
                {
                    File.WriteAllLines(tempFile, iniData.lines);

                    // Apply screenshot path
                    AuxInstallService.ApplyScreenshotPath(tempFile, savePath);

                    // Parse result
                    var resultLines = File.ReadAllLines(tempFile);
                    var parsed = AuxInstallService.ParseIni(resultLines);

                    // Check all non-SCREENSHOT sections are preserved
                    foreach (var (section, keys) in iniData.expectedSections)
                    {
                        if (section.Equals("SCREENSHOT", StringComparison.OrdinalIgnoreCase))
                        {
                            // For SCREENSHOT section, check non-SavePath keys are preserved
                            if (!parsed.ContainsKey("SCREENSHOT"))
                                return false.Label($"SCREENSHOT section missing after apply");

                            foreach (var (key, value) in keys)
                            {
                                if (key.Equals("SavePath", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                if (!parsed["SCREENSHOT"].TryGetValue(key, out var resultValue))
                                    return false.Label(
                                        $"SCREENSHOT key '{key}' missing after apply");

                                if (resultValue != value)
                                    return false.Label(
                                        $"SCREENSHOT key '{key}' changed: expected='{value}', got='{resultValue}'");
                            }
                            continue;
                        }

                        if (!parsed.ContainsKey(section))
                            return false.Label($"Section [{section}] missing after apply");

                        foreach (var (key, value) in keys)
                        {
                            if (!parsed[section].TryGetValue(key, out var resultValue))
                                return false.Label(
                                    $"Key '{key}' in [{section}] missing after apply");

                            if (resultValue != value)
                                return false.Label(
                                    $"Key '{key}' in [{section}] changed: expected='{value}', got='{resultValue}'");
                        }
                    }

                    return true.Label("OK: all non-SCREENSHOT sections and extra SCREENSHOT keys preserved");
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                }
            });
    }
}
