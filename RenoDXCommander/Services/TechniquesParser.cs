namespace RenoDXCommander.Services;

/// <summary>
/// Extracts .fx shader filenames from a ReShade Techniques= value string.
/// </summary>
public static class TechniquesParser
{
    /// <summary>
    /// Extracts the deduplicated set of .fx filenames from a Techniques= value string.
    /// Splits on commas, extracts the portion after '@', trims whitespace,
    /// skips entries without '@', and returns a case-preserving deduplicated set.
    /// </summary>
    public static HashSet<string> ExtractFxFiles(string techniquesValue)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(techniquesValue))
            return result;

        var entries = techniquesValue.Split(',');
        foreach (var entry in entries)
        {
            var trimmed = entry.Trim();
            var atIndex = trimmed.IndexOf('@');
            if (atIndex < 0 || atIndex >= trimmed.Length - 1)
                continue;

            var fxFile = trimmed[(atIndex + 1)..].Trim();
            if (fxFile.Length > 0)
                result.Add(fxFile);
        }

        return result;
    }
}
