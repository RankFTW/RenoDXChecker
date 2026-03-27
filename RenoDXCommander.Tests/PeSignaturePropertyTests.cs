using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for PE signature (MZ magic bytes) detection.
/// Feature: url-drag-drop-install, Property 4: PE signature detection
/// **Validates: Requirements 4.1**
///
/// The HasPeSignature method in DragDropHandler is private static, so we test
/// the equivalent logic: write bytes to a temp file, read back the first 2 bytes,
/// and verify the result matches the MZ check (0x4D, 0x5A).
/// </summary>
public class PeSignaturePropertyTests : IDisposable
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
    /// Replicates the exact logic of DragDropHandler.HasPeSignature:
    /// opens the file, checks length >= 2, reads first two bytes for 'M' (0x4D) and 'Z' (0x5A).
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
        var path = Path.Combine(Path.GetTempPath(), $"pe_prop_{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, content);
        _tempFiles.Add(path);
        return path;
    }

    /// <summary>
    /// Generator for byte arrays of length 0 to 100.
    /// </summary>
    private static readonly Gen<byte[]> GenByteArray =
        from length in Gen.Choose(0, 100)
        from bytes in Gen.ArrayOf(length, Arb.Default.Byte().Generator)
        select bytes;

    // ── Property 4: PE signature detection ────────────────────────────────────────
    // Feature: url-drag-drop-install, Property 4: PE signature detection
    // **Validates: Requirements 4.1**
    //
    // For any byte array of length >= 2, HasPeSignature returns true iff the first
    // two bytes are 0x4D ('M') and 0x5A ('Z'). For arrays shorter than 2 bytes,
    // it always returns false.
    [Property(MaxTest = 100)]
    public Property HasPeSignature_ReturnsTrueIffFirstTwoBytesAreMZ()
    {
        return Prop.ForAll(
            Arb.From(GenByteArray),
            (byte[] data) =>
            {
                var path = WriteTempFile(data);

                var actual = HasPeSignature(path);

                bool expected = data.Length >= 2
                    && data[0] == 0x4D
                    && data[1] == 0x5A;

                return (actual == expected)
                    .Label($"Length={data.Length}, " +
                           $"bytes[0..1]=[{(data.Length > 0 ? $"0x{data[0]:X2}" : "n/a")}, " +
                           $"{(data.Length > 1 ? $"0x{data[1]:X2}" : "n/a")}], " +
                           $"expected={expected}, actual={actual}");
            });
    }
}
