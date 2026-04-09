using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based test for Normal ReShade version regex extraction.
/// Feature: reshade-no-addon-support, Property 4: Normal ReShade version regex extraction
/// **Validates: Requirements 5.1**
/// </summary>
public class NormalReShadeVersionRegexPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a valid version component (1–3 digit number).
    /// </summary>
    private static readonly Gen<int> GenVersionPart = Gen.Choose(0, 999);

    /// <summary>
    /// Generates a valid version string like "6.7.3" or "10.0.1".
    /// Always 3-part (major.minor.patch) to match real ReShade versions.
    /// </summary>
    private static readonly Gen<string> GenVersion =
        from major in GenVersionPart
        from minor in GenVersionPart
        from patch in GenVersionPart
        select $"{major}.{minor}.{patch}";

    /// <summary>
    /// Generates surrounding HTML noise to embed the download link in.
    /// </summary>
    private static readonly Gen<string> GenHtmlPrefix =
        Gen.Elements(
            "<html><body>",
            "<div class=\"downloads\"><a href=\"",
            "Some random text before the link ",
            "<p>Latest version:</p><a href=\"");

    private static readonly Gen<string> GenHtmlSuffix =
        Gen.Elements(
            "</body></html>",
            "\">Download</a></div>",
            " and some text after",
            "\">click here</a></p>");

    // ── Fake HttpMessageHandler ───────────────────────────────────────────────

    private class FakeHtmlHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _html;

        public FakeHtmlHttpMessageHandler(string html) => _html = html;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_html)
            };
            return Task.FromResult(response);
        }
    }

    /// <summary>Stub ISevenZipExtractor that does nothing (not needed for version check).</summary>
    private class StubExtractor : ISevenZipExtractor
    {
        public void ExtractFile(string exePath, string entryName, string outputPath) { }
        public string? Find7ZipExe() => null;
        public List<string> ListEntries(string exePath) => new();
    }

    // ── Property 4: Normal ReShade version regex extraction ───────────────────
    // Feature: reshade-no-addon-support, Property 4: Normal ReShade version regex extraction
    // **Validates: Requirements 5.1**

    [Property(MaxTest = 100)]
    public Property ExtractsCorrectVersionAndUrl_FromNormalReShadeLink()
    {
        var gen =
            from version in GenVersion
            from prefix in GenHtmlPrefix
            from suffix in GenHtmlSuffix
            select (version, prefix, suffix);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (version, prefix, suffix) = tuple;
            var expectedPath = $"/downloads/ReShade_Setup_{version}.exe";
            var html = $"{prefix}{expectedPath}{suffix}";

            var handler = new FakeHtmlHttpMessageHandler(html);
            using var http = new HttpClient(handler);
            var sut = new NormalReShadeUpdateService(http, new StubExtractor());

            var result = sut.CheckLatestVersionAsync().GetAwaiter().GetResult();

            return (result != null)
                .Label($"Expected match for version '{version}' in HTML but got null")
                .And((result!.Value.version == version)
                    .Label($"Expected version '{version}' but got '{result.Value.version}'"))
                .And((result.Value.url == $"https://reshade.me{expectedPath}")
                    .Label($"Expected URL 'https://reshade.me{expectedPath}' but got '{result.Value.url}'"));
        });
    }

    [Property(MaxTest = 100)]
    public Property DoesNotMatchAddonUrls()
    {
        var gen =
            from version in GenVersion
            from prefix in GenHtmlPrefix
            from suffix in GenHtmlSuffix
            select (version, prefix, suffix);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (version, prefix, suffix) = tuple;
            // Only include an _Addon URL — no normal URL present
            var addonPath = $"/downloads/ReShade_Setup_{version}_Addon.exe";
            var html = $"{prefix}{addonPath}{suffix}";

            var handler = new FakeHtmlHttpMessageHandler(html);
            using var http = new HttpClient(handler);
            var sut = new NormalReShadeUpdateService(http, new StubExtractor());

            var result = sut.CheckLatestVersionAsync().GetAwaiter().GetResult();

            return (result == null)
                .Label($"Expected no match for Addon URL '{addonPath}' but got version='{result?.version}' url='{result?.url}'");
        });
    }
}
