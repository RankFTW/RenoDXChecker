using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: preset-shader-install, Property 2: techniques extraction completeness

/// <summary>
/// Property-based tests for TechniquesParser.ExtractFxFiles.
/// For any Techniques= value string containing known .fx filenames in TechniqueName@File.fx
/// format (with arbitrary whitespace around commas and @), ExtractFxFiles SHALL return a set
/// containing exactly those .fx filenames, deduplicated, with no extra entries and no missing entries.
/// **Validates: Requirements 7.1, 7.2, 7.5**
/// </summary>
public class TechniquesParserExtractionPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a valid technique name: non-empty alphanumeric string.
    /// </summary>
    private static Gen<string> GenTechniqueName()
    {
        return Gen.Elements(
            "SMAA", "LumaSharpen", "Clarity", "Vibrance", "Tonemap",
            "HDR", "Bloom", "AmbientLight", "MXAO", "DepthOfField",
            "ColorMatrix", "Curves", "Levels", "LiftGammaGain", "FilmGrain");
    }

    /// <summary>
    /// Generates a valid .fx shader filename.
    /// </summary>
    private static Gen<string> GenFxFile()
    {
        return Gen.Elements(
            "SMAA.fx", "LumaSharpen.fx", "Clarity.fx", "Vibrance.fx",
            "Tonemap.fx", "HDR.fx", "Bloom.fx", "AmbientLight.fx",
            "qUINT_mxao.fx", "DOF.fx", "ColorMatrix.fx", "Curves.fx");
    }

    /// <summary>
    /// Generates optional whitespace (0-3 spaces).
    /// </summary>
    private static Gen<string> GenWhitespace()
    {
        return Gen.Elements("", " ", "  ", "   ");
    }

    /// <summary>
    /// Generates a valid technique entry with optional whitespace: " TechniqueName @ File.fx ".
    /// Returns a tuple of (entry string, expected .fx filename).
    /// </summary>
    private static Gen<(string Entry, string ExpectedFx)> GenValidEntry()
    {
        return from name in GenTechniqueName()
               from fx in GenFxFile()
               from wsBefore in GenWhitespace()
               from wsAfterAt in GenWhitespace()
               from wsLeading in GenWhitespace()
               from wsTrailing in GenWhitespace()
               select ($"{wsLeading}{name}{wsBefore}@{wsAfterAt}{fx}{wsTrailing}", fx);
    }

    /// <summary>
    /// Generates an entry without @ that should be skipped by the parser.
    /// Returns (entry string, null) to indicate no .fx expected.
    /// </summary>
    private static Gen<(string Entry, string? ExpectedFx)> GenSkippableEntry()
    {
        return from name in GenTechniqueName()
               from ws in GenWhitespace()
               select ($"{ws}{name}{ws}", (string?)null);
    }

    /// <summary>
    /// Generates a mixed list of valid and skippable entries, including potential duplicates.
    /// Returns the full Techniques= value string and the expected deduplicated set of .fx filenames.
    /// </summary>
    private static Gen<(string TechniquesValue, HashSet<string> ExpectedFxFiles)> GenTechniquesInput()
    {
        return from validCount in Gen.Choose(1, 6)
               from validEntries in Gen.ListOf(validCount, GenValidEntry())
               from skipCount in Gen.Choose(0, 3)
               from skipEntries in Gen.ListOf(skipCount, GenSkippableEntry())
               from dupCount in Gen.Choose(0, 2)
               from dupEntries in Gen.ListOf(dupCount, GenValidEntry())
               let allEntries = validEntries
                   .Select(e => (e.Entry, (string?)e.ExpectedFx))
                   .Concat(skipEntries)
                   .Concat(dupEntries.Select(e => (e.Entry, (string?)e.ExpectedFx)))
                   .ToList()
               from shuffled in GenShuffle(allEntries)
               let commaWs = ", "
               let techniquesValue = string.Join(",", shuffled.Select(e => e.Entry))
               let expectedFx = new HashSet<string>(
                   shuffled.Where(e => e.Item2 != null).Select(e => e.Item2!),
                   StringComparer.Ordinal)
               select (techniquesValue, expectedFx);
    }

    /// <summary>
    /// Fisher-Yates shuffle generator for a list.
    /// </summary>
    private static Gen<List<T>> GenShuffle<T>(List<T> items)
    {
        if (items.Count <= 1)
            return Gen.Constant(items);

        return from swaps in Gen.ListOf(items.Count, Gen.Choose(0, items.Count - 1))
               let shuffled = Shuffle(items, swaps.ToList())
               select shuffled;
    }

    private static List<T> Shuffle<T>(List<T> items, List<int> swaps)
    {
        var result = new List<T>(items);
        for (int i = 0; i < result.Count && i < swaps.Count; i++)
        {
            int j = swaps[i] % result.Count;
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }

    // ── Property 2: Techniques extraction completeness ────────────────────────

    /// <summary>
    /// ExtractFxFiles returns exactly the expected deduplicated set of .fx filenames
    /// from a Techniques= value containing valid entries, skippable entries, whitespace,
    /// and duplicates.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExtractFxFiles_ReturnsExactlyExpectedDeduplicatedSet()
    {
        return Prop.ForAll(
            GenTechniquesInput().ToArbitrary(),
            input =>
            {
                var actual = TechniquesParser.ExtractFxFiles(input.TechniquesValue);
                var setsEqual = actual.SetEquals(input.ExpectedFxFiles);
                return setsEqual.Label(
                    $"Expected: {{{string.Join(", ", input.ExpectedFxFiles)}}}\n" +
                    $"Actual:   {{{string.Join(", ", actual)}}}\n" +
                    $"Input:    \"{input.TechniquesValue}\"");
            });
    }
}
