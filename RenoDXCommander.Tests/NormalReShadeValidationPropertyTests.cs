using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based test for Normal ReShade PE header and size validation.
/// Feature: reshade-no-addon-support, Property 5: PE header and size validation
/// **Validates: Requirements 5.3, 5.4**
/// </summary>
[Collection("StagingFiles")]
public class NormalReShadeValidationPropertyTests : IDisposable
{
    // The staging directory used by NormalReShadeUpdateService
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RHI", "reshade-normal");

    private static readonly string VersionFile = Path.Combine(CacheDir, "reshade_version.txt");

    /// <summary>Clean up cached files before/after each test to ensure isolation.</summary>
    public NormalReShadeValidationPropertyTests()
    {
        CleanupCacheDir();
    }

    public void Dispose()
    {
        CleanupCacheDir();
    }

    private static void CleanupCacheDir()
    {
        try
        {
            // Remove any exe files, version file, and staged DLLs that tests may have created
            if (Directory.Exists(CacheDir))
            {
                foreach (var f in Directory.GetFiles(CacheDir, "ReShade_Setup_*.exe"))
                    DeleteWithRetry(f);
                foreach (var f in Directory.GetFiles(CacheDir, "ReShade_Setup_*.exe.tmp"))
                    DeleteWithRetry(f);
                if (File.Exists(VersionFile))
                    DeleteWithRetry(VersionFile);
                var dll64 = Path.Combine(CacheDir, "ReShade64.dll");
                var dll32 = Path.Combine(CacheDir, "ReShade32.dll");
                if (File.Exists(dll64)) DeleteWithRetry(dll64);
                if (File.Exists(dll32)) DeleteWithRetry(dll32);
            }
        }
        catch { /* best effort */ }
    }

    private static void DeleteWithRetry(string path, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try { File.Delete(path); return; }
            catch (IOException) when (i < maxRetries - 1) { Thread.Sleep(50 * (i + 1)); }
            catch { return; }
        }
    }

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a byte array that starts with MZ (0x4D, 0x5A) and has length ≥ 500,000.
    /// This represents a valid PE file that passes both header and size checks.
    /// </summary>
    private static readonly Gen<byte[]> GenValidPeBytes =
        from extraLen in Gen.Choose(0, 100_000)
        let totalLen = 500_000 + extraLen
        select CreateMzBytes(totalLen);

    /// <summary>
    /// Generates a byte array that does NOT start with MZ.
    /// The first two bytes are chosen to avoid 0x4D,0x5A.
    /// Size is ≥ 500,000 so only the header check fails.
    /// </summary>
    private static readonly Gen<byte[]> GenInvalidHeaderBytes =
        from b0 in Gen.Choose(0, 255)
        from b1 in Gen.Choose(0, 255)
        where !(b0 == 0x4D && b1 == 0x5A)
        from extraLen in Gen.Choose(0, 10_000)
        let totalLen = 500_000 + extraLen
        select CreateBytesWithHeader((byte)b0, (byte)b1, totalLen);

    /// <summary>
    /// Generates a byte array that starts with MZ but is shorter than 500,000 bytes.
    /// This passes the header check but fails the size check.
    /// </summary>
    private static readonly Gen<byte[]> GenTooSmallBytes =
        from len in Gen.Choose(2, 499_999)
        select CreateMzBytes(len);

    private static byte[] CreateMzBytes(int length)
    {
        var bytes = new byte[length];
        bytes[0] = 0x4D; // 'M'
        bytes[1] = 0x5A; // 'Z'
        return bytes;
    }

    private static byte[] CreateBytesWithHeader(byte b0, byte b1, int length)
    {
        var bytes = new byte[length];
        bytes[0] = b0;
        bytes[1] = b1;
        return bytes;
    }

    // ── Fake HTTP handler ─────────────────────────────────────────────────────

    /// <summary>
    /// HTTP handler that serves an HTML page with a download link for the base URL,
    /// and returns the provided byte array for the download URL.
    /// </summary>
    private class FakeDownloadHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _downloadBytes;
        private const string Version = "1.0.0";

        public FakeDownloadHttpMessageHandler(byte[] downloadBytes)
        {
            _downloadBytes = downloadBytes;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;

            if (request.RequestUri?.AbsolutePath == "/" || request.RequestUri?.AbsoluteUri == "https://reshade.me")
            {
                // Serve HTML page with a valid download link
                var html = $"<html><body><a href=\"/downloads/ReShade_Setup_{Version}.exe\">Download</a></body></html>";
                response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(html)
                };
            }
            else
            {
                // Serve the binary download
                response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_downloadBytes)
                };
                response.Content.Headers.ContentLength = _downloadBytes.Length;
            }

            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Stub extractor that creates dummy DLL files at the output paths.
    /// This allows EnsureLatestAsync to complete the extraction step.
    /// </summary>
    private class StubExtractor : ISevenZipExtractor
    {
        public void ExtractFile(string exePath, string entryName, string outputPath)
        {
            // Create a small dummy file to simulate successful extraction
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
        }

        public string? Find7ZipExe() => null;
        public List<string> ListEntries(string exePath) => new();
    }

    // ── Property 5: PE header and size validation ─────────────────────────────
    // Feature: reshade-no-addon-support, Property 5: PE header and size validation
    // **Validates: Requirements 5.3, 5.4**

    [Property(MaxTest = 100)]
    public Property ValidPeFile_PassesValidation()
    {
        return Prop.ForAll(GenValidPeBytes.ToArbitrary(), bytes =>
        {
            CleanupCacheDir();

            var handler = new FakeDownloadHttpMessageHandler(bytes);
            using var http = new HttpClient(handler);
            var sut = new NormalReShadeUpdateService(http, new StubExtractor());

            var result = sut.EnsureLatestAsync().GetAwaiter().GetResult();

            return result
                .Label($"Expected validation to PASS for MZ-prefixed byte array of length {bytes.Length}, but EnsureLatestAsync returned false");
        });
    }

    [Property(MaxTest = 100)]
    public Property InvalidPeHeader_FailsValidation()
    {
        return Prop.ForAll(GenInvalidHeaderBytes.ToArbitrary(), bytes =>
        {
            CleanupCacheDir();

            var handler = new FakeDownloadHttpMessageHandler(bytes);
            using var http = new HttpClient(handler);
            var sut = new NormalReShadeUpdateService(http, new StubExtractor());

            var result = sut.EnsureLatestAsync().GetAwaiter().GetResult();

            return (!result)
                .Label($"Expected validation to FAIL for non-MZ header (0x{bytes[0]:X2} 0x{bytes[1]:X2}) of length {bytes.Length}, but EnsureLatestAsync returned true");
        });
    }

    [Property(MaxTest = 100)]
    public Property TooSmallFile_FailsValidation()
    {
        return Prop.ForAll(GenTooSmallBytes.ToArbitrary(), bytes =>
        {
            CleanupCacheDir();

            var handler = new FakeDownloadHttpMessageHandler(bytes);
            using var http = new HttpClient(handler);
            var sut = new NormalReShadeUpdateService(http, new StubExtractor());

            var result = sut.EnsureLatestAsync().GetAwaiter().GetResult();

            return (!result)
                .Label($"Expected validation to FAIL for MZ-prefixed byte array of length {bytes.Length} (< 500,000), but EnsureLatestAsync returned true");
        });
    }
}
