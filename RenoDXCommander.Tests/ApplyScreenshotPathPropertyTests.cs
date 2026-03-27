using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for ApplyScreenshotPath writing correct SavePath.
/// Feature: screenshot-path-settings, Property 2: ApplyScreenshotPath writes correct SavePath
/// **Validates: Requirements 4.2, 6.1**
/// </summary>
public class ApplyScreenshotPathPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a valid INI section name (alphanumeric, no brackets or newlines).
    /// </summary>
    private static readonly Gen<string> GenSectionName =
        from len in Gen.Choose(1, 12)
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
    /// Generates a valid INI key name (alphanumeric, no = or newlines).
    /// </summary>
    private static readonly Gen<string> GenKeyName =
        from len in Gen.Choose(1, 10)
        from chars in Gen.ListOf(len, Gen.Elements(
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z'))
        select new string(chars.ToArray());

    /// <summary>
    /// Generates a valid INI value (printable ASCII, no newlines).
    /// </summary>
    private static readonly Gen<string> GenValue =
        from len in Gen.Choose(0, 20)
        from chars in Gen.ListOf(len, Gen.Choose(32, 126).Select(i => (char)i))
        select new string(chars.ToArray());

    /// <summary>
    /// Generates a non-empty screenshot path string.
    /// </summary>
    private static readonly Gen<string> GenScreenshotPath =
        Gen.Elements(
            @"D:\Screenshots", @"C:\Users\Test\Pictures\ReShade",
            @"E:\Games\Screenshots", @"\\server\share\shots",
            @"D:\My Screenshots\HDR", @"C:\temp");

    /// <summary>
    /// Generates valid INI file content as a string array of lines.
    /// </summary>
    private static readonly Gen<string[]> GenIniContent =
        from sectionCount in Gen.Choose(0, 4)
        from sections in Gen.ListOf(sectionCount, GenIniSection())
        select sections.SelectMany(s => s).ToArray();

    private static Gen<string[]> GenIniSection()
    {
        return from name in GenSectionName
               from keyCount in Gen.Choose(1, 4)
               from keys in Gen.ListOf(keyCount, GenKeyValue())
               let header = $"[{name}]"
               select new[] { header }.Concat(keys).Concat(new[] { "" }).ToArray();
    }

    private static Gen<string> GenKeyValue()
    {
        return from key in GenKeyName
               from value in GenValue
               select $"{key}={value}";
    }

    // ── Property 2 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any valid INI file content and any non-empty screenshot path string,
    /// after calling ApplyScreenshotPath(iniFile, savePath), parsing the resulting
    /// INI should yield a [SCREENSHOT] section containing SavePath equal to the
    /// provided path string.
    /// **Validates: Requirements 4.2, 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ApplyScreenshotPath_Writes_Correct_SavePath()
    {
        return Prop.ForAll(
            Arb.From(GenIniContent),
            Arb.From(GenScreenshotPath),
            (string[] iniLines, string savePath) =>
            {
                var tempFile = Path.GetTempFileName();
                try
                {
                    // Write initial INI content
                    File.WriteAllLines(tempFile, iniLines);

                    // Apply screenshot path
                    AuxInstallService.ApplyScreenshotPath(tempFile, savePath);

                    // Parse result and verify
                    var resultLines = File.ReadAllLines(tempFile);
                    var parsed = AuxInstallService.ParseIni(resultLines);

                    if (!parsed.ContainsKey("SCREENSHOT"))
                        return false.Label("Missing [SCREENSHOT] section after ApplyScreenshotPath");

                    if (!parsed["SCREENSHOT"].TryGetValue("SavePath", out var resultPath))
                        return false.Label("Missing SavePath key in [SCREENSHOT] section");

                    if (resultPath != savePath)
                        return false.Label(
                            $"SavePath mismatch: expected='{savePath}', got='{resultPath}'");

                    return true.Label($"OK: SavePath='{savePath}'");
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                }
            });
    }
}
