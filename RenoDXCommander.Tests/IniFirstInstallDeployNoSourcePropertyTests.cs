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
// Feature: ini-first-install-deploy, Property 3: Deploy is a no-op when source INI is missing

/// <summary>
/// Property-based tests verifying that DeployUlIniIfAbsent and DeployDcIniIfAbsent
/// do not create any file at the deploy path and do not throw an exception when the
/// source INI file is absent from the AppData folder.
///
/// **Validates: Requirements 1.3, 2.3**
/// </summary>
public class IniFirstInstallDeployNoSourcePropertyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly byte[]? _originalUlContent;
    private readonly byte[]? _originalDcContent;
    private readonly bool _ulExistedBefore;
    private readonly bool _dcExistedBefore;

    public IniFirstInstallDeployNoSourcePropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcIniNoSrc_" + Guid.NewGuid().ToString("N")[..8]);
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

        // Remove source INI files so they are absent for all tests
        IniTestFileHelper.DeleteWithRetry(AuxInstallService.UlIniPath);
        IniTestFileHelper.DeleteWithRetry(AuxInstallService.DcIniPath);
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

    // Feature: ini-first-install-deploy, Property 3: Deploy is a no-op when source INI is missing
    /// <summary>
    /// For any game deploy path where relimiter.ini does not exist, if the source
    /// relimiter.ini is absent from the AppData folder, DeployUlIniIfAbsent does not
    /// create any file at the deploy path and does not throw an exception.
    ///
    /// **Validates: Requirements 1.3, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeployUlIniIfAbsent_NoOp_WhenSourceMissing()
    {
        return Prop.ForAll(Arb.Default.NonNegativeInt(), suffix =>
        {
            var gameDir = Path.Combine(_tempRoot, $"ul_{suffix.Get}_{Guid.NewGuid():N}"[..16]);
            Directory.CreateDirectory(gameDir);

            try
            {
                // Ensure source INI is absent
                IniTestFileHelper.DeleteWithRetry(AuxInstallService.UlIniPath);

                var destFile = Path.Combine(gameDir, "relimiter.ini");

                // Act — should not throw
                Exception? caught = null;
                try
                {
                    AuxInstallService.DeployUlIniIfAbsent(gameDir);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }

                // Assert: no exception thrown
                if (caught != null)
                    return false.Label($"Exception thrown: {caught.GetType().Name}: {caught.Message}");

                // Assert: no file created at deploy path
                if (File.Exists(destFile))
                    return false.Label("relimiter.ini was created despite missing source");

                return true.Label("No file created and no exception — correct no-op");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }

    // Feature: ini-first-install-deploy, Property 3: Deploy is a no-op when source INI is missing
    /// <summary>
    /// For any game deploy path where DisplayCommander.ini does not exist, if the source
    /// DisplayCommander.ini is absent from the AppData folder, DeployDcIniIfAbsent does not
    /// create any file at the deploy path and does not throw an exception.
    ///
    /// **Validates: Requirements 1.3, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeployDcIniIfAbsent_NoOp_WhenSourceMissing()
    {
        return Prop.ForAll(Arb.Default.NonNegativeInt(), suffix =>
        {
            var gameDir = Path.Combine(_tempRoot, $"dc_{suffix.Get}_{Guid.NewGuid():N}"[..16]);
            Directory.CreateDirectory(gameDir);

            try
            {
                // Ensure source INI is absent
                IniTestFileHelper.DeleteWithRetry(AuxInstallService.DcIniPath);

                var destFile = Path.Combine(gameDir, "DisplayCommander.ini");

                // Act — should not throw
                Exception? caught = null;
                try
                {
                    AuxInstallService.DeployDcIniIfAbsent(gameDir);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }

                // Assert: no exception thrown
                if (caught != null)
                    return false.Label($"Exception thrown: {caught.GetType().Name}: {caught.Message}");

                // Assert: no file created at deploy path
                if (File.Exists(destFile))
                    return false.Label("DisplayCommander.ini was created despite missing source");

                return true.Label("No file created and no exception — correct no-op");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }
}
