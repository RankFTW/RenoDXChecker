using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: preset-shader-install, Property 4: shader resolution correctness

/// <summary>
/// Property-based tests for ShaderResolver.Resolve.
/// For any set of required .fx filenames and for any dictionary mapping pack IDs to file lists,
/// Resolve SHALL return exactly the set of pack IDs whose file lists contain at least one of the
/// required .fx filenames (case-insensitive match), and the unresolved set SHALL contain exactly
/// those .fx filenames not found in any pack's file list.
/// **Validates: Requirements 5.2, 5.3, 5.5**
/// </summary>
public class ShaderResolverPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a valid .fx shader filename.
    /// </summary>
    private static Gen<string> GenFxFile()
    {
        return Gen.Elements(
            "SMAA.fx", "LumaSharpen.fx", "Clarity.fx", "Vibrance.fx",
            "Tonemap.fx", "HDR.fx", "Bloom.fx", "AmbientLight.fx",
            "qUINT_mxao.fx", "DOF.fx", "ColorMatrix.fx", "Curves.fx",
            "FilmGrain.fx", "Levels.fx", "LiftGammaGain.fx", "Vignette.fx");
    }

    /// <summary>
    /// Generates a pack ID string like "pack-0", "pack-1", etc.
    /// </summary>
    private static Gen<string> GenPackId()
    {
        return Gen.Elements(
            "pack-reshade", "pack-qUINT", "pack-sweetfx", "pack-marty",
            "pack-prod80", "pack-crosire", "pack-daodan", "pack-otis");
    }

    /// <summary>
    /// Generates a directory prefix for shader file paths within a pack.
    /// </summary>
    private static Gen<string> GenDirPrefix()
    {
        return Gen.Elements(
            "shaders/", "Shaders/", "reshade-shaders/Shaders/",
            "textures/", "effects/", "");
    }

    /// <summary>
    /// Generates a case variant of a filename (e.g., "SMAA.fx" → "smaa.fx" or "SMAA.FX").
    /// Used to test case-insensitive matching.
    /// </summary>
    private static Gen<string> GenCaseVariant(string fileName)
    {
        return Gen.Elements(
            fileName,
            fileName.ToLowerInvariant(),
            fileName.ToUpperInvariant());
    }

    /// <summary>
    /// Generates a pack file list: a list of file paths, some of which are .fx files with directory prefixes.
    /// Returns the list of paths and the set of .fx filenames (lowercased) contained in the pack.
    /// </summary>
    private static Gen<(IReadOnlyList<string> FileList, HashSet<string> ContainedFxLower)> GenPackFileList(
        List<string> fxPool)
    {
        var fxGen = fxPool.Count > 0
            ? Gen.Elements(fxPool.ToArray())
            : Gen.Constant("Fallback.fx");
        var maxInclude = Math.Min(3, Math.Max(1, fxPool.Count));

        return from includeCount in Gen.Choose(0, maxInclude)
               from includedFx in Gen.ListOf(includeCount, fxGen)
               from prefixes in Gen.ListOf(includeCount, GenDirPrefix())
               let paths = includedFx.Zip(prefixes, (fx, dir) => dir + fx).ToList()
               from extraNonFx in Gen.Choose(0, 3)
               from extras in Gen.ListOf(extraNonFx, Gen.Elements(
                   "readme.txt", "license.md", "textures/noise.png", "shaders/helper.fxh"))
               let allPaths = paths.Concat(extras).ToList()
               let containedFx = new HashSet<string>(
                   includedFx.Select(f => f.ToLowerInvariant()),
                   StringComparer.OrdinalIgnoreCase)
               select ((IReadOnlyList<string>)allPaths, containedFx);
    }

    /// <summary>
    /// Generates the full test input: a dictionary of pack ID → file list, a set of required .fx files
    /// (some present in packs, some not), and the expected matched pack IDs and unresolved files.
    /// </summary>
    private static Gen<TestInput> GenTestInput()
    {
        return from fxCount in Gen.Choose(4, 8)
               from fxFiles in Gen.ListOf(fxCount, GenFxFile())
                   .Select(list => list.Distinct().ToList())
               where fxFiles.Count > 0
               from packCount in Gen.Choose(1, 5)
               from packIds in Gen.ListOf(packCount, GenPackId())
                   .Select(ids => ids.Distinct().ToList())
               where packIds.Count > 0
               from packEntries in Gen.Sequence(
                   packIds.Select(id => GenPackFileList(fxFiles).Select(pl => (id, pl.FileList, pl.ContainedFxLower))))
               let packFileLists = packEntries
                   .GroupBy(e => e.id)
                   .ToDictionary(
                       g => g.Key,
                       g => g.First().FileList)
               let packFxSets = packEntries
                   .GroupBy(e => e.id)
                   .ToDictionary(
                       g => g.Key,
                       g => g.First().ContainedFxLower)
               // Pick some required files from the pool and some that won't be in any pack
               from reqCount in Gen.Choose(1, Math.Min(4, fxFiles.Count))
               from requiredFromPool in Gen.ListOf(reqCount, Gen.Elements(fxFiles.ToArray()))
                   .Select(list => list.Distinct().ToList())
               from unresolvedCount in Gen.Choose(0, 2)
               from unresolvedFiles in Gen.ListOf(unresolvedCount,
                   Gen.Elements("Missing1.fx", "NotFound.fx", "Unknown.fx", "Absent.fx"))
                   .Select(list => list.Distinct().ToList())
               // Apply case variants to required files to test case-insensitive matching
               from requiredVariants in Gen.Sequence(
                   requiredFromPool.Select(f => GenCaseVariant(f)))
               let allRequired = requiredVariants.Concat(unresolvedFiles).Distinct().ToList()
               // Compute expected results
               let expectedMatchedPacks = new HashSet<string>(
                   packFxSets
                       .Where(kvp => allRequired.Any(req =>
                           kvp.Value.Contains(req, StringComparer.OrdinalIgnoreCase)))
                       .Select(kvp => kvp.Key),
                   StringComparer.Ordinal)
               let allPackFxLower = new HashSet<string>(
                   packFxSets.Values.SelectMany(s => s),
                   StringComparer.OrdinalIgnoreCase)
               let expectedUnresolved = new HashSet<string>(
                   allRequired.Where(r => !allPackFxLower.Contains(r)),
                   StringComparer.OrdinalIgnoreCase)
               select new TestInput(
                   allRequired,
                   packFileLists.ToDictionary(
                       kvp => kvp.Key,
                       kvp => (IReadOnlyList<string>)kvp.Value) as IReadOnlyDictionary<string, IReadOnlyList<string>>,
                   expectedMatchedPacks,
                   expectedUnresolved);
    }

    // ── Test Input Record ─────────────────────────────────────────────────────────

    private record TestInput(
        List<string> RequiredFxFiles,
        IReadOnlyDictionary<string, IReadOnlyList<string>> PackFileLists,
        HashSet<string> ExpectedMatchedPacks,
        HashSet<string> ExpectedUnresolved);

    // ── Property 4: Shader resolution correctness ─────────────────────────────

    /// <summary>
    /// Resolve returns exactly the pack IDs whose file lists contain at least one required
    /// .fx file (case-insensitive), and the unresolved set contains exactly those .fx files
    /// not found in any pack.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_ReturnsCorrectMatchedPacksAndUnresolvedFiles()
    {
        return Prop.ForAll(
            GenTestInput().ToArbitrary(),
            input =>
            {
                var (matchedPackIds, unresolvedFiles) = ShaderResolver.Resolve(
                    input.RequiredFxFiles,
                    input.PackFileLists);

                var packsCorrect = matchedPackIds.SetEquals(input.ExpectedMatchedPacks);
                var unresolvedCorrect = unresolvedFiles.SetEquals(input.ExpectedUnresolved);

                return (packsCorrect && unresolvedCorrect).Label(
                    $"Required:           [{string.Join(", ", input.RequiredFxFiles)}]\n" +
                    $"Packs:              [{string.Join(", ", input.PackFileLists.Keys)}]\n" +
                    $"Expected matched:   {{{string.Join(", ", input.ExpectedMatchedPacks)}}}\n" +
                    $"Actual matched:     {{{string.Join(", ", matchedPackIds)}}}\n" +
                    $"Expected unresolved:{{{string.Join(", ", input.ExpectedUnresolved)}}}\n" +
                    $"Actual unresolved:  {{{string.Join(", ", unresolvedFiles)}}}");
            });
    }
}
