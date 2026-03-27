using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for MergeRsIni including screenshot path.
/// Feature: screenshot-path-settings, Property 7: MergeRsIni includes screenshot path
/// **Validates: Requirements 8.1, 8.2**
/// </summary>
public class MergeRsIniScreenshotPropertyTests
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
            @"E:\Games\Screenshots", @"C:\temp");

    /// <summary>
    /// Generates valid INI file content as lines.
    /// </summary>
    private static readonly Gen<string[]> GenIniContent =
        from sectionCount in Gen.Choose(1, 3)
        from sections in Gen.ListOf(sectionCount, GenIniSection())
        select sections.SelectMany(s => s).ToArray();

    private static Gen<string[]> GenIniSection()
    {
        return from name in GenSectionName
               from keyCount in Gen.Choose(1, 3)
               from keys in Gen.ListOf(keyCount, GenKeyName)
               from values in Gen.ListOf(keyCount, GenValue)
               let lines = new[] { $"[{name}]" }
                   .Concat(keys.Zip(values).Select(kv => $"{kv.First}={kv.Second}"))
                   .Concat(new[] { "" })
               select lines.ToArray();
    }

    // ── Property 7 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any valid INI template content, any valid game INI content, and any non-empty
    /// screenshot save path, after calling MergeRsIni(gameDir, screenshotSavePath), the
    /// resulting reshade.ini should contain a [SCREENSHOT] section with SavePath equal to
    /// the provided path.
    ///
    /// Since MergeRsIni reads from AuxInstallService.RsIniPath (a static path), we test
    /// at a lower level using ParseIni/WriteIni/ApplyScreenshotPath to verify the merge
    /// + screenshot path integration logic.
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergeRsIni_With_ScreenshotPath_Produces_Correct_SavePath()
    {
        return Prop.ForAll(
            Arb.From(GenIniContent),
            Arb.From(GenIniContent),
            Arb.From(GenScreenshotPath),
            (string[] templateLines, string[] gameLines, string screenshotPath) =>
            {
                var tempFile = Path.GetTempFileName();
                try
                {
                    // Simulate what MergeRsIni does:
                    // 1. Parse both template and game INI
                    var gameIni = AuxInstallService.ParseIni(gameLines);
                    var templateIni = AuxInstallService.ParseIni(templateLines);

                    // 2. Merge: template keys overwrite, game-only keys preserved
                    foreach (var (section, templateKeys) in templateIni)
                    {
                        if (!gameIni.TryGetValue(section, out var gameKeys))
                        {
                            gameIni[section] = new AuxInstallService.OrderedDict(templateKeys);
                        }
                        else
                        {
                            foreach (var (key, value) in templateKeys)
                                gameKeys[key] = value;
                        }
                    }

                    // 3. Write merged INI
                    AuxInstallService.WriteIni(tempFile, gameIni);

                    // 4. Apply screenshot path (as MergeRsIni does when screenshotSavePath != null)
                    AuxInstallService.ApplyScreenshotPath(tempFile, screenshotPath);

                    // 5. Verify result
                    var resultLines = File.ReadAllLines(tempFile);
                    var parsed = AuxInstallService.ParseIni(resultLines);

                    if (!parsed.ContainsKey("SCREENSHOT"))
                        return false.Label("Missing [SCREENSHOT] section after merge + apply");

                    if (!parsed["SCREENSHOT"].TryGetValue("SavePath", out var resultPath))
                        return false.Label("Missing SavePath key in [SCREENSHOT] section");

                    if (resultPath != screenshotPath)
                        return false.Label(
                            $"SavePath mismatch: expected='{screenshotPath}', got='{resultPath}'");

                    return true.Label($"OK: SavePath='{screenshotPath}' after merge");
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                }
            });
    }
}
