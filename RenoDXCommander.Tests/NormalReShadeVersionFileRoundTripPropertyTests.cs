using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for Normal ReShade version file round-trip.
/// Feature: reshade-no-addon-support, Property 6: Version file round-trip
/// **Validates: Requirements 5.5**
/// </summary>
[Collection("StagingFiles")]
public class NormalReShadeVersionFileRoundTripPropertyTests : IDisposable
{
    private static readonly string VersionDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RHI", "reshade-normal");

    private static readonly string VersionFile = Path.Combine(VersionDir, "reshade_version.txt");

    /// <summary>Backup of the original version file content (if any) so we can restore after tests.</summary>
    private readonly string? _originalContent;
    private readonly bool _originalExists;

    public NormalReShadeVersionFileRoundTripPropertyTests()
    {
        _originalExists = File.Exists(VersionFile);
        _originalContent = _originalExists ? File.ReadAllText(VersionFile) : null;
    }

    public void Dispose()
    {
        // Restore original state
        if (_originalExists)
            File.WriteAllText(VersionFile, _originalContent!);
        else if (File.Exists(VersionFile))
            File.Delete(VersionFile);
    }

    // ── Generators ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates non-empty version strings: digits and dots like real ReShade versions,
    /// optionally with leading/trailing whitespace to verify trimming.
    /// </summary>
    private static readonly Gen<string> GenVersionCore =
        from major in Gen.Choose(0, 999)
        from minor in Gen.Choose(0, 999)
        from patch in Gen.Choose(0, 999)
        select $"{major}.{minor}.{patch}";

    /// <summary>
    /// Wraps a version core with optional leading/trailing whitespace to test trimming.
    /// </summary>
    private static readonly Gen<string> GenVersionWithWhitespace =
        from core in GenVersionCore
        from leadingSpaces in Gen.Choose(0, 3)
        from trailingSpaces in Gen.Choose(0, 3)
        from useNewlines in Gen.Elements(false, true)
        let leading = useNewlines
            ? new string('\n', leadingSpaces)
            : new string(' ', leadingSpaces)
        let trailing = useNewlines
            ? new string('\n', trailingSpaces)
            : new string(' ', trailingSpaces)
        select $"{leading}{core}{trailing}";

    // ── Property 6: Version file round-trip ───────────────────────────────────
    // Feature: reshade-no-addon-support, Property 6: Version file round-trip
    // **Validates: Requirements 5.5**

    [Property(MaxTest = 100)]
    public Property WriteThenGetStagedVersion_ReturnsTrimmedString()
    {
        return Prop.ForAll(GenVersionWithWhitespace.ToArbitrary(), versionInput =>
        {
            var trimmed = versionInput.Trim();

            // Ensure directory exists
            Directory.CreateDirectory(VersionDir);

            // Write the version string to the file (simulating what EnsureLatestAsync does)
            File.WriteAllText(VersionFile, versionInput);

            // Read back via the static method
            var result = NormalReShadeUpdateService.GetStagedVersion();

            return (result != null)
                .Label($"GetStagedVersion() returned null after writing '{versionInput}'")
                .And((result == trimmed)
                    .Label($"Expected trimmed '{trimmed}' but got '{result}'"));
        });
    }
}
