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
// Feature: ini-first-install-deploy, Property 2: Deploy preserves existing INI

/// <summary>
/// Property-based tests verifying that DeployUlIniIfAbsent and DeployDcIniIfAbsent
/// leave an existing INI file byte-for-byte unchanged when it is already present
/// at the game deploy path.
///
/// **Validates: Requirements 1.2, 2.2, 3.1, 3.2**
/// </summary>
public class IniFirstInstallDeployPreservePropertyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly byte[]? _originalUlContent;
    private readonly byte[]? _originalDcContent;
    private readonly bool _ulExistedBefore;
    private readonly bool _dcExistedBefore;

    public IniFirstInstallDeployPreservePropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcIniPreserve_" + Guid.NewGuid().ToString("N")[..8]);
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

        try { IniTestFileHelper.ReleaseLock(); } catch { }

        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    /// <summary>
    /// Generates non-empty byte arrays to use as INI content.
    /// </summary>
    private static Arbitrary<byte[]> NonEmptyByteArray() =>
        Arb.Default.NonEmptyArray<byte>().Convert(nea => nea.Get, arr => NonEmptyArray<byte>.NewNonEmptyArray(arr));

    // Feature: ini-first-install-deploy, Property 2: Deploy preserves existing INI
    /// <summary>
    /// For any existing INI content at the deploy path and any source INI content,
    /// calling DeployUlIniIfAbsent leaves the existing relimiter.ini byte-for-byte unchanged.
    ///
    /// **Validates: Requirements 1.2, 2.2, 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeployUlIniIfAbsent_PreservesExistingFile()
    {
        return Prop.ForAll(NonEmptyByteArray(), NonEmptyByteArray(), (existingContent, sourceContent) =>
        {
            var gameDir = Path.Combine(_tempRoot, "ul_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(gameDir);

            try
            {
                // Arrange: place existing INI at the deploy path
                var destFile = Path.Combine(gameDir, "relimiter.ini");
                File.WriteAllBytes(destFile, existingContent);

                // Arrange: place source INI with different content
                IniTestFileHelper.WriteWithRetry(AuxInstallService.UlIniPath, sourceContent);

                // Act
                AuxInstallService.DeployUlIniIfAbsent(gameDir);

                // Assert: existing file is byte-for-byte unchanged
                var afterDeploy = File.ReadAllBytes(destFile);
                var unchanged = existingContent.Length == afterDeploy.Length
                    && existingContent.AsSpan().SequenceEqual(afterDeploy);

                return unchanged.Label(
                    $"Existing file was modified: before={existingContent.Length} bytes, after={afterDeploy.Length} bytes");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }

    // Feature: ini-first-install-deploy, Property 2: Deploy preserves existing INI
    /// <summary>
    /// For any existing INI content at the deploy path and any source INI content,
    /// calling DeployDcIniIfAbsent leaves the existing DisplayCommander.ini byte-for-byte unchanged.
    ///
    /// **Validates: Requirements 1.2, 2.2, 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeployDcIniIfAbsent_PreservesExistingFile()
    {
        return Prop.ForAll(NonEmptyByteArray(), NonEmptyByteArray(), (existingContent, sourceContent) =>
        {
            var gameDir = Path.Combine(_tempRoot, "dc_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(gameDir);

            try
            {
                // Arrange: place existing INI at the deploy path
                var destFile = Path.Combine(gameDir, "DisplayCommander.ini");
                File.WriteAllBytes(destFile, existingContent);

                // Arrange: place source INI with different content
                IniTestFileHelper.WriteWithRetry(AuxInstallService.DcIniPath, sourceContent);

                // Act
                AuxInstallService.DeployDcIniIfAbsent(gameDir);

                // Assert: existing file is byte-for-byte unchanged
                var afterDeploy = File.ReadAllBytes(destFile);
                var unchanged = existingContent.Length == afterDeploy.Length
                    && existingContent.AsSpan().SequenceEqual(afterDeploy);

                return unchanged.Label(
                    $"Existing file was modified: before={existingContent.Length} bytes, after={afterDeploy.Length} bytes");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }
}
