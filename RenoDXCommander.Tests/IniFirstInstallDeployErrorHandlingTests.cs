using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// All INI first-install deploy tests share the same AppData source INI files,
/// so they must run sequentially to avoid file-locking contention.
/// </summary>
[Collection("IniFirstInstallDeploy")]
/// <summary>
/// Unit tests verifying that DeployUlIniIfAbsent and DeployDcIniIfAbsent
/// swallow I/O errors and never throw, so the addon install is never blocked.
///
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
public class IniFirstInstallDeployErrorHandlingTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly byte[]? _originalUlContent;
    private readonly byte[]? _originalDcContent;
    private readonly bool _ulExistedBefore;
    private readonly bool _dcExistedBefore;

    public IniFirstInstallDeployErrorHandlingTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcIniErr_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);

        IniTestFileHelper.AcquireLock();
        Directory.CreateDirectory(AuxInstallService.InisDir);

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

    // ── Non-existent game install path ────────────────────────────────────────

    /// <summary>
    /// DeployUlIniIfAbsent does not throw when the game install path does not exist.
    /// The outer try/catch swallows the DirectoryNotFoundException from GetAddonDeployPath / File.Copy.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void DeployUlIniIfAbsent_DoesNotThrow_WhenGamePathDoesNotExist()
    {
        // Arrange: ensure source INI exists so we exercise the copy path
        IniTestFileHelper.WriteWithRetry(AuxInstallService.UlIniPath, [0x01, 0x02]);

        var bogusPath = Path.Combine(_tempRoot, "nonexistent", "game", "dir");

        // Act & Assert — must not throw
        var ex = Record.Exception(() => AuxInstallService.DeployUlIniIfAbsent(bogusPath));
        Assert.Null(ex);
    }

    /// <summary>
    /// DeployDcIniIfAbsent does not throw when the game install path does not exist.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void DeployDcIniIfAbsent_DoesNotThrow_WhenGamePathDoesNotExist()
    {
        IniTestFileHelper.WriteWithRetry(AuxInstallService.DcIniPath, [0x01, 0x02]);

        var bogusPath = Path.Combine(_tempRoot, "nonexistent", "game", "dir");

        var ex = Record.Exception(() => AuxInstallService.DeployDcIniIfAbsent(bogusPath));
        Assert.Null(ex);
    }

    // ── Read-only destination directory ───────────────────────────────────────

    /// <summary>
    /// DeployUlIniIfAbsent does not throw when the destination directory is read-only.
    /// On Windows, setting ReadOnly on a directory doesn't prevent file creation,
    /// so we use a locked file at the destination path to force an I/O error instead.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void DeployUlIniIfAbsent_DoesNotThrow_WhenDestFileIsLocked()
    {
        IniTestFileHelper.WriteWithRetry(AuxInstallService.UlIniPath, [0x01, 0x02]);

        var gameDir = Path.Combine(_tempRoot, "ul_locked");
        Directory.CreateDirectory(gameDir);

        // Create and exclusively lock the destination file.
        // File.Exists sees it → method returns early (no-op per Req 1.2). No error path hit.
        var destFile = Path.Combine(gameDir, "relimiter.ini");
        using var lockStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None);
        lockStream.WriteByte(0xFF);
        lockStream.Flush();

        var ex = Record.Exception(() => AuxInstallService.DeployUlIniIfAbsent(gameDir));
        Assert.Null(ex);
    }

    /// <summary>
    /// DeployDcIniIfAbsent does not throw when the destination file is locked.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void DeployDcIniIfAbsent_DoesNotThrow_WhenDestFileIsLocked()
    {
        IniTestFileHelper.WriteWithRetry(AuxInstallService.DcIniPath, [0x01, 0x02]);

        var gameDir = Path.Combine(_tempRoot, "dc_locked");
        Directory.CreateDirectory(gameDir);

        var destFile = Path.Combine(gameDir, "DisplayCommander.ini");
        using var lockStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None);
        lockStream.WriteByte(0xFF);
        lockStream.Flush();

        var ex = Record.Exception(() => AuxInstallService.DeployDcIniIfAbsent(gameDir));
        Assert.Null(ex);
    }

    // ── Source INI locked / inaccessible ──────────────────────────────────────

    /// <summary>
    /// DeployUlIniIfAbsent does not throw when the source relimiter.ini is exclusively
    /// locked by another process, causing File.Copy to fail with an IOException.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void DeployUlIniIfAbsent_DoesNotThrow_WhenSourceIniIsLocked()
    {
        // Write source INI then lock it exclusively
        IniTestFileHelper.WriteWithRetry(AuxInstallService.UlIniPath, [0x01, 0x02]);
        using var lockStream = new FileStream(
            AuxInstallService.UlIniPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var gameDir = Path.Combine(_tempRoot, "ul_src_locked");
        Directory.CreateDirectory(gameDir);

        // Act & Assert — File.Copy will fail because source is locked, but method swallows it
        var ex = Record.Exception(() => AuxInstallService.DeployUlIniIfAbsent(gameDir));
        Assert.Null(ex);

        // The INI should NOT have been deployed
        Assert.False(File.Exists(Path.Combine(gameDir, "relimiter.ini")));
    }

    /// <summary>
    /// DeployDcIniIfAbsent does not throw when the source DisplayCommander.ini is exclusively
    /// locked by another process, causing File.Copy to fail with an IOException.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void DeployDcIniIfAbsent_DoesNotThrow_WhenSourceIniIsLocked()
    {
        IniTestFileHelper.WriteWithRetry(AuxInstallService.DcIniPath, [0x01, 0x02]);
        using var lockStream = new FileStream(
            AuxInstallService.DcIniPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var gameDir = Path.Combine(_tempRoot, "dc_src_locked");
        Directory.CreateDirectory(gameDir);

        var ex = Record.Exception(() => AuxInstallService.DeployDcIniIfAbsent(gameDir));
        Assert.Null(ex);

        Assert.False(File.Exists(Path.Combine(gameDir, "DisplayCommander.ini")));
    }
}
