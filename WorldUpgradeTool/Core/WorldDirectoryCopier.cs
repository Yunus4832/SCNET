namespace WorldUpgradeTool.Core;

internal static class WorldDirectoryCopier
{
    public static string CreateDefaultDestinationPath(string sourceDirectoryName)
    {
        var parentDirectory = Storage.GetDirectoryName(sourceDirectoryName);
        var sourceName = Storage.GetFileName(sourceDirectoryName);
        for (var i = 0; i < 1000; i++)
        {
            var suffix = i == 0 ? ".Upgraded" : $".Upgraded{i}";
            var candidate = Storage.CombinePaths(parentDirectory, sourceName + suffix);
            if (!Storage.DirectoryExists(candidate) && !Storage.FileExists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Cannot find an unused output directory for upgraded world.");
    }

    public static void CopyWorld(string sourceDirectoryName, string destinationDirectoryName)
    {
        if (!Storage.DirectoryExists(sourceDirectoryName))
        {
            throw new DirectoryNotFoundException($"World directory not found: {sourceDirectoryName}");
        }

        if (Storage.DirectoryExists(destinationDirectoryName) || Storage.FileExists(destinationDirectoryName))
        {
            throw new InvalidOperationException($"Output directory already exists: {destinationDirectoryName}");
        }

        CopyDirectory(sourceDirectoryName, destinationDirectoryName);
    }

    private static void CopyDirectory(string sourceDirectoryName, string destinationDirectoryName)
    {
        Storage.CreateDirectory(destinationDirectoryName);

        foreach (var directoryName in Storage.ListDirectoryNames(sourceDirectoryName))
        {
            CopyDirectory(
                Storage.CombinePaths(sourceDirectoryName, directoryName),
                Storage.CombinePaths(destinationDirectoryName, directoryName));
        }

        foreach (var fileName in Storage.ListFileNames(sourceDirectoryName))
        {
            Storage.CopyFile(
                Storage.CombinePaths(sourceDirectoryName, fileName),
                Storage.CombinePaths(destinationDirectoryName, fileName));
        }
    }
}
