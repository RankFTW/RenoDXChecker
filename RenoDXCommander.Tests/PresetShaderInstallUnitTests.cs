using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for the preset-shader-install feature.
/// Covers DragDropHandler.AllowedExtensions, PresetValidator edge cases,
/// TechniquesParser edge cases, and ShaderResolver edge cases.
/// </summary>
public class PresetShaderInstallUnitTests
{
    // ── 6.1: DragDropHandler.AllowedExtensions contains .ini (Req 8.1) ──────

    [Fact]
    public void AllowedExtensions_ContainsIni()
    {
        Assert.Contains(".ini", DragDropHandler.AllowedExtensions);
    }

    // ── 6.2: PresetValidator edge cases (Req 1.3) ──────────────────────────

    [Fact]
    public void IsReShadePreset_EmptyString_ReturnsFalse()
    {
        Assert.False(PresetValidator.IsReShadePreset(string.Empty));
    }

    [Fact]
    public void IsReShadePreset_Null_ReturnsFalse()
    {
        Assert.False(PresetValidator.IsReShadePreset(null!));
    }

    [Fact]
    public void IsReShadePreset_NoTechniquesLine_ReturnsFalse()
    {
        var content = "[DX11_GLOBAL]\nPerformance=1";
        Assert.False(PresetValidator.IsReShadePreset(content));
    }

    [Fact]
    public void IsReShadePreset_TechniquesWithNoAtFxEntries_ReturnsFalse()
    {
        var content = "Techniques=SMAA,Clarity";
        Assert.False(PresetValidator.IsReShadePreset(content));
    }

    [Fact]
    public void IsReShadePreset_TechniquesWithEmptyValue_ReturnsFalse()
    {
        var content = "Techniques=";
        Assert.False(PresetValidator.IsReShadePreset(content));
    }

    [Fact]
    public void IsReShadePreset_ValidTechniques_ReturnsTrue()
    {
        var content = "Techniques=SMAA@SMAA.fx";
        Assert.True(PresetValidator.IsReShadePreset(content));
    }

    // ── 6.3: TechniquesParser edge cases (Req 7.3, 7.4) ────────────────────

    [Fact]
    public void ExtractFxFiles_EmptyString_ReturnsEmptySet()
    {
        var result = TechniquesParser.ExtractFxFiles(string.Empty);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractFxFiles_Null_ReturnsEmptySet()
    {
        var result = TechniquesParser.ExtractFxFiles(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractFxFiles_SingleEntryWithoutAt_ReturnsEmptySet()
    {
        var result = TechniquesParser.ExtractFxFiles("SMAA");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractFxFiles_WhitespaceOnly_ReturnsEmptySet()
    {
        var result = TechniquesParser.ExtractFxFiles("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractFxFiles_EntryWithAtButNothingAfter_ReturnsEmptySet()
    {
        var result = TechniquesParser.ExtractFxFiles("SMAA@");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractFxFiles_ValidSingleEntry_ReturnsFxFile()
    {
        var result = TechniquesParser.ExtractFxFiles("SMAA@SMAA.fx");
        Assert.Single(result);
        Assert.Contains("SMAA.fx", result);
    }

    // ── 6.4: ShaderResolver edge cases (Req 5.4) ───────────────────────────

    [Fact]
    public void Resolve_EmptyPackLists_AllFilesUnresolved()
    {
        var required = new[] { "SMAA.fx", "Clarity.fx" };
        var packs = new Dictionary<string, IReadOnlyList<string>>();

        var (matched, unresolved) = ShaderResolver.Resolve(required, packs);

        Assert.Empty(matched);
        Assert.Equal(2, unresolved.Count);
        Assert.Contains("SMAA.fx", unresolved);
        Assert.Contains("Clarity.fx", unresolved);
    }

    [Fact]
    public void Resolve_NoMatches_AllFilesUnresolved()
    {
        var required = new[] { "SMAA.fx", "Clarity.fx" };
        var packs = new Dictionary<string, IReadOnlyList<string>>
        {
            ["pack1"] = new List<string> { "shaders/LUT.fx", "shaders/Bloom.fx" }
        };

        var (matched, unresolved) = ShaderResolver.Resolve(required, packs);

        Assert.Empty(matched);
        Assert.Equal(2, unresolved.Count);
        Assert.Contains("SMAA.fx", unresolved);
        Assert.Contains("Clarity.fx", unresolved);
    }

    [Fact]
    public void Resolve_EmptyRequiredFiles_EmptyResults()
    {
        var required = Array.Empty<string>();
        var packs = new Dictionary<string, IReadOnlyList<string>>
        {
            ["pack1"] = new List<string> { "shaders/SMAA.fx" }
        };

        var (matched, unresolved) = ShaderResolver.Resolve(required, packs);

        Assert.Empty(matched);
        Assert.Empty(unresolved);
    }

    [Fact]
    public void Resolve_NullInputs_EmptyResults()
    {
        var (matched, unresolved) = ShaderResolver.Resolve(null!, null!);

        Assert.Empty(matched);
        Assert.Empty(unresolved);
    }
}
