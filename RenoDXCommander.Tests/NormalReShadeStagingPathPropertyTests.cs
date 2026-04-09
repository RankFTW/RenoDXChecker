using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for staging path and AddonType selection.
/// Feature: reshade-no-addon-support, Property 7: Staging path and AddonType selection
/// **Validates: Requirements 6.4, 6.5, 9.1**
/// </summary>
[Collection("StagingFiles")]
public class NormalReShadeStagingPathPropertyTests : IDisposable
{
    // ── Staging directories (real %LocalAppData% paths used by AuxInstallService) ──
    private static readonly string AddonStagingDir = AuxInstallService.RsStagingDir;
    private static readonly string NormalStagingDir = AuxInstallService.RsNormalStagingDir;

    // Unique marker bytes so we can tell which staged DLL was copied.
    // Addon 64-bit: 0xAA repeated, Addon 32-bit: 0xBB repeated,
    // Normal 64-bit: 0xCC repeated, Normal 32-bit: 0xDD repeated.
    private static readonly byte[] Addon64Marker  = Enumerable.Repeat((byte)0xAA, 64).ToArray();
    private static readonly byte[] Addon32Marker  = Enumerable.Repeat((byte)0xBB, 64).ToArray();
    private static readonly byte[] Normal64Marker = Enumerable.Repeat((byte)0xCC, 64).ToArray();
    private static readonly byte[] Normal32Marker = Enumerable.Repeat((byte)0xDD, 64).ToArray();

    /// <summary>Backup of original staging files so we can restore after tests.</summary>
    private readonly Dictionary<string, byte[]?> _originalFiles = new();
    private readonly List<string> _tempGameDirs = new();

    public NormalReShadeStagingPathPropertyTests()
    {
        // Back up and seed all four staging DLLs
        SeedStagingFile(AuxInstallService.RsStagedPath64, Addon64Marker);
        SeedStagingFile(AuxInstallService.RsStagedPath32, Addon32Marker);
        SeedStagingFile(AuxInstallService.RsNormalStagedPath64, Normal64Marker);
        SeedStagingFile(AuxInstallService.RsNormalStagedPath32, Normal32Marker);
    }

    public void Dispose()
    {
        // Restore original staging files with retry logic
        foreach (var (path, original) in _originalFiles)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (original != null)
                        WriteWithRetry(path, original);
                    else if (File.Exists(path))
                        File.Delete(path);
                    break; // success
                }
                catch
                {
                    if (attempt < 4) Thread.Sleep(100 * (attempt + 1));
                    /* best effort */
                }
            }

            // Verify restoration
            try
            {
                if (original != null)
                {
                    if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(original))
                        WriteWithRetry(path, original);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { /* best effort */ }
        }

        // Clean up temp game directories
        foreach (var dir in _tempGameDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private void SeedStagingFile(string path, byte[] content)
    {
        _originalFiles[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteWithRetry(path, content);
    }

    private static void WriteWithRetry(string path, byte[] content, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try { File.WriteAllBytes(path, content); return; }
            catch (IOException) when (i < maxRetries - 1) { Thread.Sleep(50 * (i + 1)); }
        }
    }

    private string CreateTempGameDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"RdxcTest_Staging_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempGameDirs.Add(dir);
        return dir;
    }

    // ── Generator: all four (useNormalReShade, is32Bit) combinations ──────────

    private static readonly Gen<(bool useNormal, bool is32Bit)> GenCombination =
        from useNormal in Gen.Elements(true, false)
        from is32Bit in Gen.Elements(true, false)
        select (useNormal, is32Bit);

    // ── Property 7: Staging path and AddonType selection ─────────────────────
    // Feature: reshade-no-addon-support, Property 7: Staging path and AddonType selection
    // **Validates: Requirements 6.4, 6.5, 9.1**

    [Property(MaxTest = 100)]
    public Property InstallReShadeAsync_CopiesFromCorrectStagingDir_And_SetsCorrectAddonType()
    {
        return Prop.ForAll(GenCombination.ToArbitrary(), combo =>
        {
            var (useNormal, is32Bit) = combo;
            var gameDir = CreateTempGameDir();

            var shaderStub = new TestHelpers.StubShaderPackService();
            var sut = new AuxInstallService(new HttpClient(), shaderStub);

            var record = sut.InstallReShadeAsync(
                gameName: "TestGame",
                installPath: gameDir,
                use32Bit: is32Bit,
                useNormalReShade: useNormal
            ).GetAwaiter().GetResult();

            // ── Verify AddonType ──
            var expectedType = useNormal
                ? AuxInstallService.TypeReShadeNormal
                : AuxInstallService.TypeReShade;

            var addonTypeCorrect = (record.AddonType == expectedType)
                .Label($"Expected AddonType='{expectedType}' but got '{record.AddonType}' " +
                       $"(useNormal={useNormal}, is32Bit={is32Bit})");

            // ── Verify the DLL was copied from the correct staging path ──
            var destPath = Path.Combine(gameDir, record.InstalledAs);
            var copiedBytes = File.ReadAllBytes(destPath);

            byte[] expectedMarker = (useNormal, is32Bit) switch
            {
                (true, false)  => Normal64Marker,
                (true, true)   => Normal32Marker,
                (false, false) => Addon64Marker,
                (false, true)  => Addon32Marker,
            };

            var contentCorrect = copiedBytes.SequenceEqual(expectedMarker)
                .Label($"DLL content mismatch — expected {(useNormal ? "Normal" : "Addon")} " +
                       $"{(is32Bit ? "32" : "64")}-bit marker " +
                       $"(useNormal={useNormal}, is32Bit={is32Bit})");

            return addonTypeCorrect.And(contentCorrect);
        });
    }
}
