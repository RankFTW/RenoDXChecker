using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for screenshot section idempotence.
/// Feature: screenshot-path-settings, Property 6: Screenshot section idempotence
/// **Validates: Requirements 6.2, 6.3**
/// </summary>
public class ScreenshotSectionIdempotencePropertyTests
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

    private static readonly Gen<string> GenValue =
        from len in Gen.Choose(0, 15)
        from chars in Gen.ListOf(len, Gen.Choose(32, 126).Select(i => (char)i))
        select new string(chars.ToArray());

    private static readonly Gen<string> GenScreenshotPath =
        Gen.Elements(
            @"D:\Screenshots", @"C:\Users\Test\Pictures",
            @"E:\Games\Screenshots", @"C:\temp",
            @"D:\My Shots", @"F:\ReShade\Captures");

    /// <summary>
    /// Generates INI content that may or may not already contain a [SCREENSHOT] section.
    /// </summary>
    private static readonly Gen<string[]> GenIniContent =
        from includeScreenshot in Arb.Default.Bool().Generator
        from sectionCount in Gen.Choose(0, 3)
        from sections in Gen.ListOf(sectionCount, GenIniSection())
        from screenshotSection in includeScreenshot
            ? GenScreenshotSection()
            : Gen.Constant(Array.Empty<string>())
        select sections.SelectMany(s => s).Concat(screenshotSection).ToArray();

    private static Gen<string[]> GenIniSection()
    {
        return from name in GenSectionName.Where(s =>
                   !s.Equals("SCREENSHOT", StringComparison.OrdinalIgnoreCase))
               from keyCount in Gen.Choose(1, 3)
               from keys in Gen.ListOf(keyCount, GenKeyName)
               from values in Gen.ListOf(keyCount, GenValue)
               let lines = new[] { $"[{name}]" }
                   .Concat(keys.Zip(values).Select(kv => $"{kv.First}={kv.Second}"))
                   .Concat(new[] { "" })
               select lines.ToArray();
    }

    private static Gen<string[]> GenScreenshotSection()
    {
        return from existingPath in GenScreenshotPath
               from extraKeyCount in Gen.Choose(0, 2)
               from extraKeys in Gen.ListOf(extraKeyCount,
                   GenKeyName.Where(k => !k.Equals("SavePath", StringComparison.OrdinalIgnoreCase)))
               from extraValues in Gen.ListOf(extraKeyCount, GenValue)
               let lines = new[] { "[SCREENSHOT]", $"SavePath={existingPath}" }
                   .Concat(extraKeys.Zip(extraValues).Select(kv => $"{kv.First}={kv.Second}"))
                   .Concat(new[] { "" })
               select lines.ToArray();
    }

    // ── Property 6 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any valid INI file content (with or without an existing [SCREENSHOT] section),
    /// after calling ApplyScreenshotPath, the resulting INI should contain exactly one
    /// [SCREENSHOT] section. Calling ApplyScreenshotPath a second time with a different
    /// path should still result in exactly one [SCREENSHOT] section with the new path.
    /// **Validates: Requirements 6.2, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ApplyScreenshotPath_Is_Idempotent_On_Section_Count()
    {
        return Prop.ForAll(
            Arb.From(GenIniContent),
            Arb.From(GenScreenshotPath),
            Arb.From(GenScreenshotPath),
            (string[] iniLines, string path1, string path2) =>
            {
                var tempFile = Path.GetTempFileName();
                try
                {
                    File.WriteAllLines(tempFile, iniLines);

                    // First apply
                    AuxInstallService.ApplyScreenshotPath(tempFile, path1);

                    var resultLines1 = File.ReadAllLines(tempFile);
                    var screenshotCount1 = CountScreenshotSections(resultLines1);

                    if (screenshotCount1 != 1)
                        return false.Label(
                            $"After first apply: expected 1 [SCREENSHOT] section, found {screenshotCount1}");

                    var parsed1 = AuxInstallService.ParseIni(resultLines1);
                    if (!parsed1.ContainsKey("SCREENSHOT") ||
                        !parsed1["SCREENSHOT"].TryGetValue("SavePath", out var sp1) ||
                        sp1 != path1)
                        return false.Label(
                            $"After first apply: SavePath mismatch, expected='{path1}'");

                    // Second apply with different path
                    AuxInstallService.ApplyScreenshotPath(tempFile, path2);

                    var resultLines2 = File.ReadAllLines(tempFile);
                    var screenshotCount2 = CountScreenshotSections(resultLines2);

                    if (screenshotCount2 != 1)
                        return false.Label(
                            $"After second apply: expected 1 [SCREENSHOT] section, found {screenshotCount2}");

                    var parsed2 = AuxInstallService.ParseIni(resultLines2);
                    if (!parsed2.ContainsKey("SCREENSHOT"))
                        return false.Label("After second apply: [SCREENSHOT] section missing");

                    if (!parsed2["SCREENSHOT"].TryGetValue("SavePath", out var sp2))
                        return false.Label("After second apply: SavePath key missing");

                    if (sp2 != path2)
                        return false.Label(
                            $"After second apply: SavePath mismatch, expected='{path2}', got='{sp2}'");

                    return true.Label(
                        $"OK: path1='{path1}', path2='{path2}', always exactly 1 [SCREENSHOT] section");
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                }
            });
    }

    /// <summary>
    /// Counts the number of [SCREENSHOT] section headers in the raw INI lines.
    /// </summary>
    private static int CountScreenshotSections(string[] lines)
    {
        return lines.Count(line =>
        {
            var trimmed = line.Trim();
            return trimmed.StartsWith('[') && trimmed.Contains(']') &&
                   trimmed.Trim('[', ']', ' ').Equals("SCREENSHOT", StringComparison.OrdinalIgnoreCase);
        });
    }
}
