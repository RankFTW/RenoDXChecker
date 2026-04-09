using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: preset-shader-install, Property 3: techniques extraction order independence

/// <summary>
/// Property-based tests for TechniquesParser.ExtractFxFiles order independence.
/// For any valid Techniques= value string, shuffling the comma-separated entries
/// SHALL produce the same .fx filename set as the original ordering.
/// **Validates: Requirements 7.6**
/// </summary>
public class TechniquesParserOrderPropertyTests
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
    /// Generates a single valid technique entry: "TechniqueName @ File.fx" with optional whitespace.
    /// </summary>
    private static Gen<string> GenValidEntry()
    {
        return from name in GenTechniqueName()
               from fx in GenFxFile()
               from wsBefore in GenWhitespace()
               from wsAfterAt in GenWhitespace()
               from wsLeading in GenWhitespace()
               from wsTrailing in GenWhitespace()
               select $"{wsLeading}{name}{wsBefore}@{wsAfterAt}{fx}{wsTrailing}";
    }

    /// <summary>
    /// Generates a list of valid technique entries (2-8 entries) and returns
    /// both the original ordering and a shuffled ordering as Techniques= value strings.
    /// </summary>
    private static Gen<(string Original, string Shuffled)> GenOriginalAndShuffled()
    {
        return from count in Gen.Choose(2, 8)
               from entries in Gen.ListOf(count, GenValidEntry())
               let entryList = entries.ToList()
               from shuffled in GenShuffle(entryList)
               let original = string.Join(",", entryList)
               let shuffledStr = string.Join(",", shuffled)
               select (original, shuffledStr);
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

    // ── Property 3: Techniques extraction order independence ──────────────────

    /// <summary>
    /// Shuffling the comma-separated entries of a Techniques= value string
    /// produces the same .fx filename set as the original ordering.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExtractFxFiles_OrderIndependent_SameSet()
    {
        return Prop.ForAll(
            GenOriginalAndShuffled().ToArbitrary(),
            input =>
            {
                var originalSet = TechniquesParser.ExtractFxFiles(input.Original);
                var shuffledSet = TechniquesParser.ExtractFxFiles(input.Shuffled);
                var setsEqual = originalSet.SetEquals(shuffledSet);
                return setsEqual.Label(
                    $"Original set:  {{{string.Join(", ", originalSet)}}}\n" +
                    $"Shuffled set:  {{{string.Join(", ", shuffledSet)}}}\n" +
                    $"Original input:  \"{input.Original}\"\n" +
                    $"Shuffled input:  \"{input.Shuffled}\"");
            });
    }
}
