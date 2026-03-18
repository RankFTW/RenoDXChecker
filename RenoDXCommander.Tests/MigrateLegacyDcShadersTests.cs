using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for ShaderPackService.MigrateLegacyDcShaders.
/// Uses the internal overload that accepts explicit directory paths
/// so tests operate on isolated temp directories.
///
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**
/// </summary>
public class MigrateLegacyDcShadersTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _shadersDir;
    private readonly string _texturesDir;

    public MigrateLegacyDcShadersTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcMigrate_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
        _shadersDir = Path.Combine(_tempRoot, "Shaders");
        _texturesDir = Path.Combine(_tempRoot, "Textures");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    /// <summary>
    /// Validates: Requirement 2.1
    /// WHEN Shaders exists and Shaders.old does not, it is renamed.
    /// </summary>
    [Fact]
    public void RenamesShaders_WhenShadersExists_AndShadersOldDoesNot()
    {
        Directory.CreateDirectory(_shadersDir);
        File.WriteAllText(Path.Combine(_shadersDir, "test.fx"), "// shader");

        ShaderPackService.MigrateLegacyDcShaders(_shadersDir, _texturesDir);

        Assert.False(Directory.Exists(_shadersDir));
        Assert.True(Directory.Exists(_shadersDir + ".old"));
        Assert.True(File.Exists(Path.Combine(_shadersDir + ".old", "test.fx")));
    }

    /// <summary>
    /// Validates: Requirement 2.2
    /// WHEN Textures exists and Textures.old does not, it is renamed.
    /// </summary>
    [Fact]
    public void RenamesTextures_WhenTexturesExists_AndTexturesOldDoesNot()
    {
        Directory.CreateDirectory(_texturesDir);
        File.WriteAllText(Path.Combine(_texturesDir, "test.png"), "tex");

        ShaderPackService.MigrateLegacyDcShaders(_shadersDir, _texturesDir);

        Assert.False(Directory.Exists(_texturesDir));
        Assert.True(Directory.Exists(_texturesDir + ".old"));
        Assert.True(File.Exists(Path.Combine(_texturesDir + ".old", "test.png")));
    }

    /// <summary>
    /// Validates: Requirements 2.3, 2.4
    /// WHEN .old already exists, the rename is skipped and the original folder is preserved.
    /// </summary>
    [Fact]
    public void SkipsRename_WhenOldAlreadyExists()
    {
        // Set up Shaders + Shaders.old
        Directory.CreateDirectory(_shadersDir);
        File.WriteAllText(Path.Combine(_shadersDir, "new.fx"), "// new");
        Directory.CreateDirectory(_shadersDir + ".old");
        File.WriteAllText(Path.Combine(_shadersDir + ".old", "old.fx"), "// old");

        // Set up Textures + Textures.old
        Directory.CreateDirectory(_texturesDir);
        File.WriteAllText(Path.Combine(_texturesDir, "new.png"), "new");
        Directory.CreateDirectory(_texturesDir + ".old");
        File.WriteAllText(Path.Combine(_texturesDir + ".old", "old.png"), "old");

        ShaderPackService.MigrateLegacyDcShaders(_shadersDir, _texturesDir);

        // Original folders still exist (rename was skipped)
        Assert.True(Directory.Exists(_shadersDir));
        Assert.True(Directory.Exists(_texturesDir));

        // .old folders are unchanged
        Assert.True(File.Exists(Path.Combine(_shadersDir + ".old", "old.fx")));
        Assert.True(File.Exists(Path.Combine(_texturesDir + ".old", "old.png")));
    }

    /// <summary>
    /// Validates: Requirement 2.5
    /// WHEN directories are absent, the method does not crash.
    /// </summary>
    [Fact]
    public void DoesNotCrash_WhenDirectoriesAreAbsent()
    {
        // Neither Shaders nor Textures exist — should complete without exception
        var ex = Record.Exception(() =>
            ShaderPackService.MigrateLegacyDcShaders(_shadersDir, _texturesDir));

        Assert.Null(ex);
    }
}
