using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// All INI first-install deploy tests share the same AppData source INI files,
/// so they must run sequentially to avoid file-locking contention.
/// </summary>
[Collection("IniFirstInstallDeploy")]
// Feature: ini-first-install-deploy, Property 1: Deploy copies INI when destination is absent

/// <summary>
/// Property-based tests verifying that DeployUlIniIfAbsent and DeployDcIniIfAbsent
/// copy the source INI to the game deploy path when the destination file is absent.
///
/// **Validates: Requirements 1.1, 2.1**
/// </summary>
public class IniFirstInstallDeployCopyPropertyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly byte[]? _originalUlContent;
    private readonly byte[]? _originalDcContent;
    private readonly bool _ulExistedBefore;
    private readonly bool _dcExistedBefore;

    public IniFirstInstallDeployCopyPropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcIniCopy_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);

        // Acquire exclusive access to shared source INI files
        IniTestFileHelper.AcquireLock();

        // Ensure inis directory exists
        Directory.CreateDirectory(AuxInstallService.InisDir);

        // Back up any existing source INI files
        _ulExistedBefore = File.Exists(AuxInstallService.UlIniPath);
        _originalUlContent = _ulExistedBefore ? IniTestFileHelper.ReadWithRetry(AuxInstallService.UlIniPath) : null;

        _dcExistedBefore = File.Exists(AuxInstallService.DcIniPath);
        _originalDcContent = _dcExistedBefore ? IniTestFileHelper.ReadWithRetry(AuxInstallService.DcIniPath) : null;
    }

    public void Dispose()
    {
        try
        {
            // Restore original source INI files
            if (_ulExistedBefore && _originalUlContent != null)
                IniTestFileHelper.WriteWithRetry(AuxInstallService.UlIniPath, _originalUlContent);
            else if (!_ulExistedBefore)
                IniTestFileHelper.DeleteWithRetry(AuxInstallService.UlIniPath);
        }
        catch { }

        try
        {
            if (_dcExistedBefore && _originalDcContent != null)
                IniTestFileHelper.WriteWithRetry(AuxInstallService.DcIniPath, _originalDcContent);
            else if (!_dcExistedBefore)
                IniTestFileHelper.DeleteWithRetry(AuxInstallService.DcIniPath);
        }
        catch { }

        // Release the shared mutex
        try { IniTestFileHelper.ReleaseLock(); } catch { }

        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    /// <summary>
    /// Generates non-empty byte arrays to use as INI content.
    /// </summary>
    private static Arbitrary<byte[]> NonEmptyByteArray() =>
        Arb.Default.NonEmptyArray<byte>().Convert(nea => nea.Get, arr => NonEmptyArray<byte>.NewNonEmptyArray(arr));

    // Feature: ini-first-install-deploy, Property 1: Deploy copies INI when destination is absent
    /// <summary>
    /// For any non-empty INI content, when the source relimiter.ini exists and the
    /// destination does not, DeployUlIniIfAbsent copies the file with identical content.
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeployUlIniIfAbsent_CopiesFile_WhenDestinationAbsent()
    {
        return Prop.ForAll(NonEmptyByteArray(), content =>
        {
            var gameDir = Path.Combine(_tempRoot, "ul_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(gameDir);

            try
            {
                // Arrange: place source INI with generated content
                IniTestFileHelper.WriteWithRetry(AuxInstallService.UlIniPath, content);

                // Ensure destination does not exist
                var destFile = Path.Combine(gameDir, "relimiter.ini");
                if (File.Exists(destFile)) File.Delete(destFile);

                // Act
                AuxInstallService.DeployUlIniIfAbsent(gameDir);

                // Assert: file exists at deploy path
                if (!File.Exists(destFile))
                    return false.Label("relimiter.ini was not created at deploy path");

                // Assert: content is identical
                var deployed = File.ReadAllBytes(destFile);
                var match = content.Length == deployed.Length
                    && content.AsSpan().SequenceEqual(deployed);

                return match.Label(
                    $"Content mismatch: source={content.Length} bytes, deployed={deployed.Length} bytes");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }

    // Feature: ini-first-install-deploy, Property 1: Deploy copies INI when destination is absent
    /// <summary>
    /// For any non-empty INI content, when the source DisplayCommander.ini exists and the
    /// destination does not, DeployDcIniIfAbsent copies the file with identical content.
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeployDcIniIfAbsent_CopiesFile_WhenDestinationAbsent()
    {
        return Prop.ForAll(NonEmptyByteArray(), content =>
        {
            var gameDir = Path.Combine(_tempRoot, "dc_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(gameDir);

            try
            {
                // Arrange: place source INI with generated content
                IniTestFileHelper.WriteWithRetry(AuxInstallService.DcIniPath, content);

                // Ensure destination does not exist
                var destFile = Path.Combine(gameDir, "DisplayCommander.ini");
                if (File.Exists(destFile)) File.Delete(destFile);

                // Act
                AuxInstallService.DeployDcIniIfAbsent(gameDir);

                // Assert: file exists at deploy path
                if (!File.Exists(destFile))
                    return false.Label("DisplayCommander.ini was not created at deploy path");

                // Assert: content is identical
                var deployed = File.ReadAllBytes(destFile);
                var match = content.Length == deployed.Length
                    && content.AsSpan().SequenceEqual(deployed);

                return match.Label(
                    $"Content mismatch: source={content.Length} bytes, deployed={deployed.Length} bytes");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }
}
