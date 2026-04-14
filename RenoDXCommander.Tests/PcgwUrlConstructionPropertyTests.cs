// Feature: nexus-pcgw-integration, Property 6: PCGW URL construction
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for PCGW URL construction.
/// For any positive integer AppID, the constructed URL equals
/// "https://www.pcgamingwiki.com/api/appid.php?appid={appId}".
/// For any non-empty page title, the constructed URL equals
/// "https://www.pcgamingwiki.com/wiki/{title}" with spaces replaced by underscores.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
///
/// **Validates: Requirements 7.2, 7.4**
/// </summary>
public class PcgwUrlConstructionPropertyTests
{
    /// <summary>
    /// Generates a positive integer AppID (Steam AppIDs are positive).
    /// </summary>
    private static Gen<int> PositiveAppIdGen()
    {
        return Gen.Choose(1, int.MaxValue);
    }

    /// <summary>
    /// Generates a non-empty page title string.
    /// </summary>
    private static Gen<string> NonEmptyPageTitleGen()
    {
        return Arb.Default.NonEmptyString().Generator.Select(s => s.Get);
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 6: PCGW URL construction
    ///
    /// **Validates: Requirements 7.2**
    ///
    /// For any positive integer AppID, BuildAppIdUrl SHALL return
    /// "https://www.pcgamingwiki.com/api/appid.php?appid={appId}".
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildAppIdUrl_ReturnsCorrectUrl()
    {
        return Prop.ForAll(PositiveAppIdGen().ToArbitrary(), appId =>
        {
            var expected = $"https://www.pcgamingwiki.com/api/appid.php?appid={appId}";
            var result = PcgwService.BuildAppIdUrl(appId);

            return (result == expected).Label(
                $"Expected '{expected}' but got '{result}' for AppID {appId}");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 6: PCGW URL construction
    ///
    /// **Validates: Requirements 7.4**
    ///
    /// For any non-empty page title, BuildWikiUrl SHALL return
    /// "https://www.pcgamingwiki.com/wiki/{title}" with spaces replaced by underscores.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildWikiUrl_ReturnsCorrectUrl_WithSpacesAsUnderscores()
    {
        return Prop.ForAll(NonEmptyPageTitleGen().ToArbitrary(), pageTitle =>
        {
            var expectedTitle = pageTitle.Replace(' ', '_');
            var expected = $"https://www.pcgamingwiki.com/wiki/{expectedTitle}";
            var result = PcgwService.BuildWikiUrl(pageTitle);

            return (result == expected).Label(
                $"Expected '{expected}' but got '{result}' for title '{pageTitle}'");
        });
    }
}
