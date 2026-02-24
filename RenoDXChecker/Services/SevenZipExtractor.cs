using System.IO.Compression;

namespace RenoDXCommander.Services;

/// <summary>
/// Extracts a named file from the ReShade installer exe.
///
/// The ReShade installer is a standard .NET executable with a ZIP archive appended
/// to the end of the PE file (verified against ReShade_Setup_6.7.2_Addon.exe).
/// System.IO.Compression.ZipFile finds an appended ZIP by locating the EOCD record
/// from the end of the file — the standard ZIP spec — so ZipFile.OpenRead(exePath)
/// just works with no scanning, no offsets, no external libraries.
///
/// Archive contents: ReShade32.dll, ReShade64.dll, and matching .json files.
/// </summary>
public static class ReShadeExtractor
{
    public static void ExtractFile(string exePath, string entryName, string outputPath)
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"ReShade installer not found: {exePath}");

        using var zip = ZipFile.OpenRead(exePath);

        var entry = zip.Entries.FirstOrDefault(e =>
            e.Name.Equals(entryName, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            var names = string.Join(", ", zip.Entries.Select(e => e.Name));
            throw new FileNotFoundException(
                $"'{entryName}' not found in installer.\nAvailable: [{names}]");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var src  = entry.Open();
        using var dest = File.Create(outputPath);
        src.CopyTo(dest);
    }
}
