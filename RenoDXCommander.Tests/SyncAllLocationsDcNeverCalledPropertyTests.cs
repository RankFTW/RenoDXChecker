using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests verifying that <c>SyncShadersToAllLocations</c> never
/// invokes <c>SyncDcFolder</c> — i.e. no shader or texture files are ever
/// written to the DC AppData folder during a global sync.
///
/// **Validates: Requirements 8.2**
///
/// Uses the real <c>ShaderPackService</c> with temp directories and verifies
/// that the DC AppData Shaders/Textures directories are never created or
/// modified by <c>SyncShadersToAllLocations</c>.
/// </summary>
[Collection("StaticShaderMode")]
public class SyncAllLocationsDcNeverCalledPropertyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly ShaderPackService _service;
    private readonly List<string> _stagedFiles = new();

    // Snapshot of DC folder state before each test run
    private readonly HashSet<string> _dcShadersSnapshot;
    private readonly HashSet<string> _dcTexturesSnapshot;

    public SyncAllLocationsDcNeverCalledPropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcDcNever_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
        _service = new ShaderPackService(new HttpClient());
        EnsureStagingFiles();

        // Snapshot the DC folders before any test runs
        _dcShadersSnapshot = SnapshotDirectory(ShaderPackService.DcShadersDir);
        _dcTexturesSnapshot = SnapshotDirectory(ShaderPackService.DcTexturesDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        foreach (var f in _stagedFiles)
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    private void EnsureStagingFiles()
    {
        Directory.CreateDirectory(ShaderPackService.ShadersDir);
        Directory.CreateDirectory(ShaderPackService.TexturesDir);

        var shaderFile = Path.Combine(ShaderPackService.ShadersDir, "_rdxc_test_dcnever.fx");
        if (!File.Exists(shaderFile))
        {
            File.WriteAllText(shaderFile, "// test shader for DC-never-called property tests");
            _stagedFiles.Add(shaderFile);
        }

        var textureFile = Path.Combine(ShaderPackService.TexturesDir, "_rdxc_test_dcnever.png");
        if (!File.Exists(textureFile))
        {
            File.WriteAllBytes(textureFile, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            _stagedFiles.Add(textureFile);
        }
    }

    /// <summary>
    /// Returns a set of all file paths (relative) inside a directory, or empty if it doesn't exist.
    /// </summary>
    private static HashSet<string> SnapshotDirectory(string dir)
    {
        if (!Directory.Exists(dir))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => f.Substring(dir.Length).TrimStart(Path.DirectorySeparatorChar))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // ── Generators ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generator for any DeployMode value (including Off).
    /// </summary>
    private static Gen<ShaderPackService.DeployMode> GenAnyDeployMode()
    {
        return Gen.Elements(
            ShaderPackService.DeployMode.Off,
            ShaderPackService.DeployMode.Minimum,
            ShaderPackService.DeployMode.All);
    }

    /// <summary>
    /// Generator for a single game location tuple with all booleans varying freely.
    /// </summary>
    private Gen<(string installPath, bool dcInstalled, bool rsInstalled, bool dcMode, string? shaderModeOverride)>
        GenLocation(int suffix)
    {
        return from dcInstalled in Arb.Default.Bool().Generator
               from rsInstalled in Arb.Default.Bool().Generator
               from dcMode in Arb.Default.Bool().Generator
               select (
                   installPath: Path.Combine(_tempRoot, $"game_{suffix}"),
                   dcInstalled,
                   rsInstalled,
                   dcMode,
                   shaderModeOverride: (string?)null
               );
    }

    // ── Property: SyncDcFolder is never called ───────────────────────────────

    /// <summary>
    /// **Validates: Requirements 8.2**
    ///
    /// For any set of game locations passed to <c>SyncShadersToAllLocations</c>,
    /// the DC AppData folder's Shaders and Textures directories SHALL NOT have
    /// any new files created — proving <c>SyncDcFolder</c> was never invoked
    /// with actual file-system side effects.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SyncShadersToAllLocations_NeverWritesToDcFolder()
    {
        var gen = from mode in GenAnyDeployMode()
                  from count in Gen.Choose(1, 5)
                  from suffix in Gen.Choose(1, 999999)
                  from dcInstalled in Arb.Default.Bool().Generator
                  from rsInstalled in Arb.Default.Bool().Generator
                  from dcMode in Arb.Default.Bool().Generator
                  select (mode, count, suffix, dcInstalled, rsInstalled, dcMode);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (mode, count, baseSuffix, dcInstalled, rsInstalled, dcMode) = tuple;

            // Build a list of game locations
            var locations = new List<(string installPath, bool dcInstalled, bool rsInstalled, bool dcMode, string? shaderModeOverride)>();
            var gameDirs = new List<string>();

            for (int i = 0; i < count; i++)
            {
                var gameDir = Path.Combine(_tempRoot, $"game_{baseSuffix}_{i}_{dcInstalled}_{rsInstalled}_{dcMode}_{mode}");
                Directory.CreateDirectory(gameDir);
                gameDirs.Add(gameDir);
                locations.Add((gameDir, dcInstalled, rsInstalled, dcMode, (string?)null));
            }

            ShaderPackService.CurrentMode = mode;

            try
            {
                _service.SyncShadersToAllLocations(locations, mode);

                // Assert: DC Shaders directory has no new files
                var currentDcShaders = SnapshotDirectory(ShaderPackService.DcShadersDir);
                var newShaderFiles = currentDcShaders.Except(_dcShadersSnapshot).ToList();
                if (newShaderFiles.Count > 0)
                    return false.Label(
                        $"DC Shaders folder was modified — new files: {string.Join(", ", newShaderFiles)} " +
                        $"(mode={mode}, dcInstalled={dcInstalled}, rsInstalled={rsInstalled}, dcMode={dcMode})");

                // Assert: DC Textures directory has no new files
                var currentDcTextures = SnapshotDirectory(ShaderPackService.DcTexturesDir);
                var newTextureFiles = currentDcTextures.Except(_dcTexturesSnapshot).ToList();
                if (newTextureFiles.Count > 0)
                    return false.Label(
                        $"DC Textures folder was modified — new files: {string.Join(", ", newTextureFiles)} " +
                        $"(mode={mode}, dcInstalled={dcInstalled}, rsInstalled={rsInstalled}, dcMode={dcMode})");

                return true.Label(
                    $"OK: mode={mode}, locations={count}, dcInstalled={dcInstalled}, rsInstalled={rsInstalled}, dcMode={dcMode}");
            }
            finally
            {
                foreach (var dir in gameDirs)
                    try { Directory.Delete(dir, recursive: true); } catch { }
            }
        });
    }
}
