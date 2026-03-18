using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests verifying that <c>SyncShadersToAllLocations</c> always
/// routes RS-installed games to <c>SyncGameFolder</c>, regardless of DC status.
///
/// Uses the real <c>ShaderPackService</c> with temp directories so we verify
/// actual file-system effects rather than a pure model.
/// </summary>
[Collection("StaticShaderMode")]
public class SyncAllLocationsRsRoutingPropertyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly ShaderPackService _service;
    private readonly List<string> _stagedFiles = new();

    public SyncAllLocationsRsRoutingPropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcRsRoute_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
        _service = new ShaderPackService(new HttpClient());
        EnsureStagingFiles();
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

        var shaderFile = Path.Combine(ShaderPackService.ShadersDir, "_rdxc_test_rsroute.fx");
        if (!File.Exists(shaderFile))
        {
            File.WriteAllText(shaderFile, "// test shader for RS routing property tests");
            _stagedFiles.Add(shaderFile);
        }

        var textureFile = Path.Combine(ShaderPackService.TexturesDir, "_rdxc_test_rsroute.png");
        if (!File.Exists(textureFile))
        {
            File.WriteAllBytes(textureFile, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            _stagedFiles.Add(textureFile);
        }
    }

    // ── Generators ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generator for non-Off DeployMode values so shaders are actually deployed.
    /// </summary>
    private static Gen<ShaderPackService.DeployMode> GenNonOffDeployMode()
    {
        return Gen.Elements(
            ShaderPackService.DeployMode.Minimum,
            ShaderPackService.DeployMode.All);
    }

    /// <summary>
    /// Generator for a game location tuple where rsInstalled is always true,
    /// but dcInstalled and dcMode vary freely.
    /// </summary>
    private Gen<(string installPath, bool dcInstalled, bool rsInstalled, bool dcMode, string? shaderModeOverride)>
        GenRsInstalledLocation(string suffix)
    {
        return from dcInstalled in Arb.Default.Bool().Generator
               from dcMode in Arb.Default.Bool().Generator
               select (
                   installPath: Path.Combine(_tempRoot, $"game_{suffix}"),
                   dcInstalled,
                   rsInstalled: true,
                   dcMode,
                   shaderModeOverride: (string?)null
               );
    }

    // ── Property: RS-installed games always get SyncGameFolder ────────────────

    /// <summary>
    /// **Validates: Requirements 8.1, 8.4**
    ///
    /// For any game location tuple where <c>rsInstalled=true</c>, regardless of
    /// <c>dcInstalled</c> or <c>dcMode</c>, <c>SyncShadersToAllLocations</c>
    /// SHALL call <c>SyncGameFolder</c> with the game's install path — verified
    /// by the presence of the RDXC-managed <c>reshade-shaders</c> folder with
    /// the managed marker file.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RsInstalled_AlwaysGetsSyncGameFolder_RegardlessOfDcStatus()
    {
        var gen = from dcInstalled in Arb.Default.Bool().Generator
                  from dcMode in Arb.Default.Bool().Generator
                  from mode in GenNonOffDeployMode()
                  from suffix in Gen.Choose(1, 999999)
                  select (dcInstalled, dcMode, mode, suffix);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (dcInstalled, dcMode, mode, suffix) = tuple;

            var gameDir = Path.Combine(_tempRoot, $"game_{suffix}_{dcInstalled}_{dcMode}_{mode}");
            Directory.CreateDirectory(gameDir);

            ShaderPackService.CurrentMode = mode;

            try
            {
                var locations = new[]
                {
                    (installPath: gameDir, dcInstalled, rsInstalled: true,
                     dcMode, shaderModeOverride: (string?)null)
                };

                _service.SyncShadersToAllLocations(locations, mode);

                // Assert: reshade-shaders folder exists (proves SyncGameFolder was called)
                var rsDir = Path.Combine(gameDir, ShaderPackService.GameReShadeShaders);
                if (!Directory.Exists(rsDir))
                    return false.Label(
                        $"reshade-shaders folder missing — SyncGameFolder was not called " +
                        $"(dcInstalled={dcInstalled}, dcMode={dcMode}, mode={mode})");

                // Assert: managed marker exists (confirms RDXC deployment)
                var markerPath = Path.Combine(rsDir, "Managed by RDXC.txt");
                if (!File.Exists(markerPath))
                    return false.Label(
                        $"Managed marker missing — SyncGameFolder did not complete deployment " +
                        $"(dcInstalled={dcInstalled}, dcMode={dcMode}, mode={mode})");

                // Assert: shader files were actually deployed
                var shadersDir = Path.Combine(rsDir, "Shaders");
                var hasShaders = Directory.Exists(shadersDir) &&
                                 Directory.EnumerateFiles(shadersDir, "*", SearchOption.AllDirectories).Any();
                if (!hasShaders)
                    return false.Label(
                        $"No shader files deployed — SyncGameFolder deployment incomplete " +
                        $"(dcInstalled={dcInstalled}, dcMode={dcMode}, mode={mode})");

                return true.Label($"OK: dcInstalled={dcInstalled}, dcMode={dcMode}, mode={mode}");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }
}
