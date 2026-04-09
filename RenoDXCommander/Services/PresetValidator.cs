namespace RenoDXCommander.Services;

/// <summary>
/// Validates whether file content represents a genuine ReShade preset INI.
/// </summary>
public static class PresetValidator
{
    /// <summary>
    /// Returns true if the file content contains a Techniques= line
    /// with at least one TechniqueName@ShaderFile.fx entry.
    /// Case-insensitive on the "Techniques" key name.
    /// </summary>
    public static bool IsReShadePreset(string fileContent)
    {
        if (string.IsNullOrEmpty(fileContent))
            return false;

        using var reader = new StringReader(fileContent);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.TrimStart();
            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex < 0)
                continue;

            var key = trimmed[..eqIndex].TrimEnd();
            if (!key.Equals("Techniques", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = trimmed[(eqIndex + 1)..];
            var entries = value.Split(',');
            foreach (var entry in entries)
            {
                var e = entry.Trim();
                var atIndex = e.IndexOf('@');
                if (atIndex < 0 || atIndex >= e.Length - 1)
                    continue;

                var fxPart = e[(atIndex + 1)..].Trim();
                if (fxPart.EndsWith(".fx", StringComparison.OrdinalIgnoreCase) && fxPart.Length > 3)
                    return true;
            }
        }

        return false;
    }
}
