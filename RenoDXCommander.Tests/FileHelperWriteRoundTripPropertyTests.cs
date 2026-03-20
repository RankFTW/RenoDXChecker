using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: file-write-retry-helper, Property 1: Write round trip
// **Validates: Requirements 1.2**

/// <summary>
/// Property-based tests for FileHelper.WriteAllTextWithRetry write round-trip.
/// For any valid temp file path and any non-null string content,
/// WriteAllTextWithRetry followed by File.ReadAllText returns identical content.
/// </summary>
public class FileHelperWriteRoundTripPropertyTests : IDisposable
{
    private readonly string _tempDir;

    public FileHelperWriteRoundTripPropertyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileHelperTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    /// <summary>
    /// Generates non-null string content including empty, whitespace, unicode, and larger strings.
    /// </summary>
    private static readonly Gen<string> GenContent =
        Gen.OneOf(
            Gen.Constant(""),
            Gen.Constant("   "),
            Gen.Constant("\t\n\r"),
            Arb.Default.NonNull<string>().Generator.Select(s => s.Get),
            Gen.ArrayOf(Gen.Choose(0x20, 0xD7FF))
                .Select(chars => new string(chars.Select(c => (char)c).ToArray())),
            Gen.Constant(new string('x', 10_000))
        );

    [Property(MaxTest = 100)]
    public Property WriteAndRead_RoundTrip_ReturnsIdenticalContent()
    {
        return Prop.ForAll(
            Arb.From(GenContent),
            (string content) =>
            {
                var filePath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.txt");

                FileHelper.WriteAllTextWithRetry(filePath, content, "Test.RoundTrip");

                var readBack = File.ReadAllText(filePath);

                return (readBack == content)
                    .Label($"Content mismatch: wrote {content.Length} chars, read back {readBack.Length} chars");
            });
    }
}
