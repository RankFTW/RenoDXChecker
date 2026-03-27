using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for PE validation (MZ magic bytes) edge cases used by DragDropHandler.
/// The HasPeSignature method is private static, so we replicate the same logic here:
/// open the file, check length >= 2, read first two bytes for 'M' (0x4D) and 'Z' (0x5A).
///
/// Validates: Requirements 4.1
/// </summary>
public class PeValidationEdgeCaseTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    /// <summary>
    /// Replicates the exact logic of DragDropHandler.HasPeSignature.
    /// </summary>
    private static bool HasPeSignature(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (fs.Length < 2) return false;
            return fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
        }
        catch { return false; }
    }

    private string WriteTempFile(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pe_edge_{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void ZeroByteFile_ReturnsFalse()
    {
        var path = WriteTempFile(Array.Empty<byte>());

        var result = HasPeSignature(path);

        Assert.False(result, "A 0-byte file should not be recognized as a PE binary");
    }

    [Fact]
    public void OneByteFile_ReturnsFalse()
    {
        var path = WriteTempFile(new byte[] { 0x4D }); // only 'M', missing 'Z'

        var result = HasPeSignature(path);

        Assert.False(result, "A 1-byte file should not be recognized as a PE binary");
    }

    [Fact]
    public void ValidMzHeader_ReturnsTrue()
    {
        var path = WriteTempFile(new byte[] { 0x4D, 0x5A }); // 'M', 'Z'

        var result = HasPeSignature(path);

        Assert.True(result, "A file starting with MZ magic bytes should be recognized as a PE binary");
    }
}
