using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Updated exploration test for DC shader deploy on install.
/// After the local-only routing change, <c>InstallDcAsync</c> calls
/// <c>SyncGameFolder</c> instead of <c>SyncDcFolder</c> for all
/// <c>dcModeLevel</c> values, including 0.
///
/// **Validates: Requirements 4.2, 4.3**
///
/// EXPECTED OUTCOME: Test PASSES — SyncGameFolder is called, SyncDcFolder is NOT called.
/// </summary>
[Collection("StaticShaderMode")]
public class DcShaderDeployExplorationTests : IDisposable
{
    private readonly string _tempRoot;

    public DcShaderDeployExplorationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcDcTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    /// <summary>
    /// Calls <c>InstallDcAsync</c> with <c>dcModeLevel=0</c> and asserts that
    /// <c>SyncGameFolder</c> is called (local deployment) and <c>SyncDcFolder</c>
    /// is NOT called (DC global folder is never synced).
    ///
    /// **Validates: Requirements 4.2, 4.3**
    /// </summary>
    [Fact]
    public async Task DcModeLevel0_ShouldCallSyncGameFolder_NotSyncDcFolder()
    {
        // Arrange
        var installPath = Path.Combine(_tempRoot, "GameFolder");
        Directory.CreateDirectory(installPath);

        // Ensure the download cache directory exists
        Directory.CreateDirectory(AuxInstallService.DownloadCacheDir);

        // Pre-seed the DC cache file so the download path is skipped
        var cachePath = Path.Combine(AuxInstallService.DownloadCacheDir, AuxInstallService.DcCacheFile);
        var fakeBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        File.WriteAllBytes(cachePath, fakeBytes);

        // Set a known global shader mode
        ShaderPackService.CurrentMode = ShaderPackService.DeployMode.Minimum;

        // Create a tracking shader pack service to record calls
        var tracker = new TrackingShaderPackService();

        var handler = new FakeHttpMessageHandler(fakeBytes);
        using var http = new HttpClient(handler);

        var sut = new AuxInstallService(http, tracker);

        // Act
        await sut.InstallDcAsync(
            gameName: "TestGame",
            installPath: installPath,
            dcModeLevel: 0);

        // Assert — SyncGameFolder is called (local deployment)
        Assert.True(
            tracker.SyncGameFolderCalled,
            "SyncGameFolder should be called during DC install for dcModeLevel == 0");
        Assert.Equal(installPath, tracker.SyncGameFolderDir);

        // Assert — SyncDcFolder is NOT called (DC global folder is never synced)
        Assert.False(
            tracker.SyncDcFolderCalled,
            "SyncDcFolder should NOT be called during DC install — local-only routing");
    }

    // ── Tracking IShaderPackService ───────────────────────────────────────────

    private class TrackingShaderPackService : IShaderPackService
    {
        public bool SyncDcFolderCalled { get; private set; }
        public ShaderPackService.DeployMode? SyncDcFolderMode { get; private set; }
        public bool SyncGameFolderCalled { get; private set; }
        public string? SyncGameFolderDir { get; private set; }
        public ShaderPackService.DeployMode? SyncGameFolderMode { get; private set; }
        public bool RemoveFromGameFolderCalled { get; private set; }
        public string? RemoveFromGameFolderDir { get; private set; }

        public IReadOnlyList<(string Id, string DisplayName, ShaderPackService.PackCategory Category)> AvailablePacks { get; } =
            new List<(string, string, ShaderPackService.PackCategory)>();

        public string? GetPackDescription(string packId) => null;

        public Task EnsureLatestAsync(IProgress<string>? progress = null) => Task.CompletedTask;
        public void DeployToDcFolder(ShaderPackService.DeployMode? mode = null) { }
        public void DeployToGameFolder(string gameDir, ShaderPackService.DeployMode? mode = null) { }

        public void RemoveFromGameFolder(string gameDir)
        {
            RemoveFromGameFolderCalled = true;
            RemoveFromGameFolderDir = gameDir;
        }

        public bool IsManagedByRdxc(string gameDir) => false;
        public void RestoreOriginalIfPresent(string gameDir) { }

        public void SyncDcFolder(ShaderPackService.DeployMode m, IEnumerable<string>? selectedPackIds = null)
        {
            SyncDcFolderCalled = true;
            SyncDcFolderMode = m;
        }

        public void SyncGameFolder(string gameDir, ShaderPackService.DeployMode m,
            IEnumerable<string>? selectedPackIds = null)
        {
            SyncGameFolderCalled = true;
            SyncGameFolderDir = gameDir;
            SyncGameFolderMode = m;
        }

        public void SyncShadersToAllLocations(
            IEnumerable<(string installPath, bool dcInstalled, bool rsInstalled, bool dcMode,
                string? shaderModeOverride)> locations,
            ShaderPackService.DeployMode? mode = null,
            IEnumerable<string>? selectedPackIds = null) { }
    }

    // ── Fake HttpMessageHandler ───────────────────────────────────────────────

    /// <summary>
    /// Returns a successful response with the given content for any request.
    /// HEAD requests return Content-Length matching the content size.
    /// </summary>
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _content;

        public FakeHttpMessageHandler(byte[] content) => _content = content;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

            if (request.Method == HttpMethod.Head)
            {
                response.Content = new ByteArrayContent(Array.Empty<byte>());
                response.Content.Headers.ContentLength = _content.Length;
            }
            else
            {
                response.Content = new ByteArrayContent(_content);
                response.Content.Headers.ContentLength = _content.Length;
            }

            return Task.FromResult(response);
        }
    }
}
