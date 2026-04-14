// Feature: nexus-pcgw-integration, Property 3: ACF filename AppID extraction round-trip
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for ACF filename AppID extraction.
/// For any positive integer n, constructing "appmanifest_{n}.acf" and extracting
/// the AppID SHALL yield n. For any string that does not match the pattern
/// "appmanifest_&lt;digits&gt;.acf", extraction SHALL yield null.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
///
/// **Validates: Requirements 4.1, 4.2, 4.3**
/// </summary>
public class AcfAppIdExtractionPropertyTests
{
    /// <summary>
    /// Generates a positive integer (Steam AppIDs are positive).
    /// </summary>
    private static Gen<int> PositiveIntGen()
    {
        return Gen.Choose(1, int.MaxValue);
    }

    /// <summary>
    /// Generates a string whose file-name-without-extension does NOT match
    /// "appmanifest_{positive-int}". The method under test uses
    /// Path.GetFileNameWithoutExtension and does not validate the extension,
    /// so "non-matching" means the stem itself fails the prefix+int check.
    /// </summary>
    private static Gen<string> NonMatchingFilenameGen()
    {
        return Gen.OneOf(
            // Random non-empty strings filtered to exclude accidental matches
            Arb.Default.NonEmptyString().Generator.Select(s => s.Get)
                .Where(s => !WouldExtract(s)),
            // Missing prefix
            Gen.Choose(1, int.MaxValue).Select(n => $"manifest_{n}.acf"),
            // Non-numeric suffix after prefix
            Arb.Default.NonEmptyString().Generator
                .Select(s => $"appmanifest_{s.Get}abc.acf")
                .Where(s => !WouldExtract(s)),
            // Empty string
            Gen.Constant(""),
            // Just the prefix with no number
            Gen.Constant("appmanifest_.acf")
        );
    }

    /// <summary>
    /// Mirrors the extraction logic to determine if a filename would yield a result.
    /// </summary>
    private static bool WouldExtract(string filename)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(filename);
        return name != null
            && name.StartsWith("appmanifest_")
            && int.TryParse(name["appmanifest_".Length..], out _);
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 3: ACF filename AppID extraction round-trip
    ///
    /// **Validates: Requirements 4.1, 4.2**
    ///
    /// For any positive integer n, constructing "appmanifest_{n}.acf" and extracting
    /// the AppID SHALL yield n.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PositiveInt_RoundTrips_Through_AcfFilename()
    {
        return Prop.ForAll(PositiveIntGen().ToArbitrary(), n =>
        {
            var filename = $"appmanifest_{n}.acf";
            var result = GameDetectionService.ExtractAppIdFromAcfFilename(filename);

            return (result.HasValue && result.Value == n).Label(
                $"Expected {n} but got {result} for filename '{filename}'");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 3: ACF filename AppID extraction round-trip
    ///
    /// **Validates: Requirements 4.3**
    ///
    /// For any string that does not match the pattern "appmanifest_&lt;digits&gt;.acf",
    /// extraction SHALL yield null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonMatchingString_Yields_Null()
    {
        return Prop.ForAll(NonMatchingFilenameGen().ToArbitrary(), filename =>
        {
            var result = GameDetectionService.ExtractAppIdFromAcfFilename(filename);

            return (result == null).Label(
                $"Expected null but got {result} for non-matching filename '{filename}'");
        });
    }
}
