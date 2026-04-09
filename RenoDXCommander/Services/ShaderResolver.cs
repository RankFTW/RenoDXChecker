namespace RenoDXCommander.Services;

/// <summary>
/// Resolves which shader packs contain the .fx files required by a ReShade preset.
/// </summary>
public static class ShaderResolver
{
    /// <summary>
    /// Given a set of required .fx filenames and a dictionary mapping pack IDs
    /// to their file lists, returns the pack IDs that contain at least one
    /// required .fx file, plus the set of unresolved .fx files not found in any pack.
    /// Case-insensitive matching on .fx filenames.
    /// </summary>
    public static (HashSet<string> MatchedPackIds, HashSet<string> UnresolvedFiles) Resolve(
        IEnumerable<string> requiredFxFiles,
        IReadOnlyDictionary<string, IReadOnlyList<string>> packFileLists)
    {
        var matchedPackIds = new HashSet<string>(StringComparer.Ordinal);
        var unresolvedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (requiredFxFiles is null || packFileLists is null)
            return (matchedPackIds, unresolvedFiles);

        foreach (var fxFile in requiredFxFiles)
        {
            if (string.IsNullOrWhiteSpace(fxFile))
                continue;

            bool found = false;

            foreach (var (packId, fileList) in packFileLists)
            {
                if (fileList is null)
                    continue;

                foreach (var entry in fileList)
                {
                    if (entry is null)
                        continue;

                    var fileName = Path.GetFileName(entry);
                    if (fileName.Equals(fxFile, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedPackIds.Add(packId);
                        found = true;
                        break; // found in this pack, move to next pack
                    }
                }
            }

            if (!found)
                unresolvedFiles.Add(fxFile);
        }

        return (matchedPackIds, unresolvedFiles);
    }
}
