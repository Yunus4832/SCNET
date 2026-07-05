namespace WorldUpgradeTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2 || !string.Equals(args[0], "upgrade", StringComparison.OrdinalIgnoreCase))
        {
            PrintUsage();
            return 2;
        }

        var worldDirectory = ToStoragePath(args[1]);
        if (!Storage.DirectoryExists(worldDirectory))
        {
            Console.Error.WriteLine($"World directory not found: {args[1]}");
            return 1;
        }

        try
        {
            var name = WorldUpgradeManager.ReadWorldName(worldDirectory);
            var beforeVersion = WorldUpgradeManager.ReadWorldVersion(worldDirectory);

            Console.WriteLine($"World: {name}");
            Console.WriteLine($"Current version: {beforeVersion}");
            Console.WriteLine($"Target version: {WorldUpgradeManager.TargetWorldSerializationVersion}");

            WorldUpgradeManager.UpgradeWorld(worldDirectory);

            var afterVersion = WorldUpgradeManager.ReadWorldVersion(worldDirectory);
            Console.WriteLine($"Upgraded version: {afterVersion}");
            return afterVersion == WorldUpgradeManager.TargetWorldSerializationVersion ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static string ToStoragePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return "system:" + fullPath;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  WorldUpgradeTool upgrade <world-directory>");
    }
}
