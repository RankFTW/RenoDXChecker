using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: preset-shader-install, Property 1: Preset validation correctness

/// <summary>
/// Property-based tests for PresetValidator.IsReShadePreset.
/// For any string content, IsReShadePreset returns true if and only if the content
/// contains a line starting with Techniques= (case-insensitive key match) whose value
/// includes at least one entry containing @ followed by a .fx filename.
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// </summary>
public class PresetValidatorPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a valid technique name: non-empty alphanumeric string.
    /// </summary>
    private static Gen<string> GenValidTechniqueName()
    {
        return Gen.Elements(
            "SMAA", "LumaSharpen", "Clarity", "Vibrance", "Tonemap",
            "HDR", "Bloom", "AmbientLight", "MXAO", "DepthOfField",
            "ColorMatrix", "Curves", "Levels", "LiftGammaGain", "FilmGrain");
    }

    /// <summary>
    /// Generates a valid .fx shader filename: alphanumeric name ending in .fx.
    /// </summary>
    private static Gen<string> GenValidFxFile()
    {
        return Gen.Elements(
            "SMAA.fx", "LumaSharpen.fx", "Clarity.fx", "Vibrance.fx",
            "Tonemap.fx", "HDR.fx", "Bloom.fx", "AmbientLight.fx",
            "qUINT_mxao.fx", "DOF.fx", "ColorMatrix.fx", "Curves.fx");
    }

    /// <summary>
    /// Generates a single valid technique entry: "TechniqueName@ShaderFile.fx".
    /// </summary>
    private static Gen<string> GenValidTechniqueEntry()
    {
        return from name in GenValidTechniqueName()
               from fx in GenValidFxFile()
               select $"{name}@{fx}";
    }

    /// <summary>
    /// Generates a valid Techniques= line with 1-5 technique entries.
    /// </summary>
    private static Gen<string> GenValidTechniquesLine()
    {
        return from count in Gen.Choose(1, 5)
               from entries in Gen.ListOf(count, GenValidTechniqueEntry())
               from keyCase in Gen.Elements("Techniques", "techniques", "TECHNIQUES", "TeChnIqUeS")
               select $"{keyCase}={string.Join(",", entries)}";
    }

    /// <summary>
    /// Generates random non-Techniques INI lines (section headers, other keys, comments, blank lines).
    /// </summary>
    private static Gen<string> GenNonTechniquesLine()
    {
        return Gen.Elements(
            "[DX11_GLOBAL]",
            "[GENERAL]",
            "PreprocessorDefinitions=RESHADE_DEPTH_INPUT_IS_UPSIDE_DOWN=0",
            "Performance=1",
            "; This is a comment",
            "",
            "KeyEffects=46,0,0,0",
            "TechniqueSorting=SMAA,Clarity",  // Not "Techniques=" — different key
            "PresetPath=C:\\presets\\my.ini",
            "[APP]",
            "ForceVSync=0");
    }

    /// <summary>
    /// Generates INI content that contains a valid Techniques= line (positive case).
    /// Wraps the valid line with random non-Techniques lines before and after.
    /// </summary>
    private static Gen<string> GenValidPresetContent()
    {
        return from beforeCount in Gen.Choose(0, 5)
               from beforeLines in Gen.ListOf(beforeCount, GenNonTechniquesLine())
               from techniquesLine in GenValidTechniquesLine()
               from afterCount in Gen.Choose(0, 5)
               from afterLines in Gen.ListOf(afterCount, GenNonTechniquesLine())
               let allLines = beforeLines.Append(techniquesLine).Concat(afterLines)
               select string.Join("\n", allLines);
    }

    /// <summary>
    /// Generates INI content that does NOT contain a valid Techniques= line (negative case).
    /// May contain no Techniques line at all, or a Techniques line with no @.fx entries.
    /// </summary>
    private static Gen<string> GenInvalidContent()
    {
        var noTechniquesLine = from count in Gen.Choose(1, 8)
                               from lines in Gen.ListOf(count, GenNonTechniquesLine())
                               select string.Join("\n", lines);

        var techniquesWithoutFx = from keyCase in Gen.Elements("Techniques", "techniques", "TECHNIQUES")
                                  from value in Gen.Elements(
                                      "",                          // empty value
                                      "SMAA,Clarity",              // no @ at all
                                      "SMAA@,Clarity@",            // @ but no .fx
                                      "SMAA@shader.txt",           // wrong extension
                                      "SMAA@.fx")                  // @ followed by .fx but no filename before .fx (length <= 3)
                                  from beforeCount in Gen.Choose(0, 3)
                                  from beforeLines in Gen.ListOf(beforeCount, GenNonTechniquesLine())
                                  from afterCount in Gen.Choose(0, 3)
                                  from afterLines in Gen.ListOf(afterCount, GenNonTechniquesLine())
                                  let allLines = beforeLines
                                      .Append($"{keyCase}={value}")
                                      .Concat(afterLines)
                                  select string.Join("\n", allLines);

        return Gen.OneOf(noTechniquesLine, techniquesWithoutFx);
    }

    // ── Property 1: Preset validation correctness ─────────────────────────────

    /// <summary>
    /// Valid preset content (containing Techniques= with @.fx entries) must be
    /// accepted by IsReShadePreset.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidPresetContent_IsAccepted()
    {
        return Prop.ForAll(
            GenValidPresetContent().ToArbitrary(),
            content =>
            {
                var result = PresetValidator.IsReShadePreset(content);
                return result.Label(
                    $"Expected IsReShadePreset=true for content with valid Techniques= line, got false.\nContent:\n{content}");
            });
    }

    /// <summary>
    /// Invalid content (no Techniques= line or Techniques= without @.fx entries)
    /// must be rejected by IsReShadePreset.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidContent_IsRejected()
    {
        return Prop.ForAll(
            GenInvalidContent().ToArbitrary(),
            content =>
            {
                var result = PresetValidator.IsReShadePreset(content);
                return (!result).Label(
                    $"Expected IsReShadePreset=false for invalid content, got true.\nContent:\n{content}");
            });
    }
}
