using WorldUpgradeTool.Core;

namespace WorldUpgradeTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        RegisterStorageRoots();
        if (args.Length is < 2 or > 3)
        {
            PrintUsage();
            return 2;
        }

        var command = args[0];
        var worldDirectory = ToStoragePath(args[1]);
        var outputDirectory = args.Length == 3 ? ToOptionalStoragePath(args[2]) : null;
        if (!Storage.DirectoryExists(worldDirectory))
        {
            Console.Error.WriteLine($"World directory not found: {args[1]}");
            return 1;
        }

        try
        {
            return command.ToLowerInvariant() switch
            {
                "inspect" when outputDirectory == null => Inspect(worldDirectory),
                "plan" when outputDirectory == null => Plan(worldDirectory),
                "upgrade" => Upgrade(worldDirectory, outputDirectory),
                _ => PrintInvalidCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void RegisterStorageRoots()
    {
        var appPath = AppContext.BaseDirectory;
        Storage.RegisterFileSystemRoot("app", appPath);
        Storage.RegisterFileSystemRoot("data", Path.Combine(appPath, "Data"));
        Storage.RegisterFileSystemRoot("system", Path.GetPathRoot(appPath) ?? appPath, allowEscapingRoot: true);
    }

    private static int Inspect(string worldDirectory)
    {
        var inspection = new WorldInspector().Inspect(worldDirectory);
        PrintInspection(inspection);
        return 0;
    }

    private static int Plan(string worldDirectory)
    {
        var context = new WorldContext(worldDirectory);
        PrintInspection(context.Inspection);

        var plan = new WorldMaintenanceRunner().PlanUpgrade(worldDirectory);
        Console.WriteLine("Plan:");
        if (plan.Steps.Count == 0)
        {
            Console.WriteLine("  <no steps>");
            return 0;
        }

        foreach (var step in plan.Steps)
        {
            Console.WriteLine($"  [{step.Kind}] {step.DisplayName} ({step.Id})");
        }

        return 0;
    }

    private static int Upgrade(string worldDirectory, string? outputDirectory)
    {
        var before = new WorldInspector().Inspect(worldDirectory);
        PrintInspection(before);
        Console.WriteLine($"Target project version: {WorldUpgradeManager.TargetProjectFormatVersion}");
        Console.WriteLine($"Target terrain storage version: {WorldUpgradeManager.TargetTerrainStorageVersion}");

        var upgradedWorldDirectory = outputDirectory != null
            ? WorldUpgradeManager.UpgradeWorld(worldDirectory, outputDirectory)
            : WorldUpgradeManager.UpgradeWorld(worldDirectory);

        var after = new WorldInspector().Inspect(upgradedWorldDirectory);
        Console.WriteLine($"Output world directory: {Storage.GetSystemPath(upgradedWorldDirectory)}");
        Console.WriteLine($"Upgraded project version: {after.ProjectVersion}");
        return after.ProjectVersion == WorldUpgradeManager.TargetProjectFormatVersion ? 0 : 1;
    }

    private static void PrintInspection(WorldInspection inspection)
    {
        Console.WriteLine($"World: {inspection.WorldName}");
        Console.WriteLine($"Current project version: {inspection.ProjectVersion}");
        Console.WriteLine($"Project.xml: {(inspection.HasProjectXml ? "yes" : "no")}");
        Console.WriteLine($"Project.json: {(inspection.HasProjectJson ? "yes" : "no")}");
        Console.WriteLine($"Regions: {(inspection.HasRegionsDirectory ? "yes" : "no")}");
    }

    private static int PrintInvalidCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 2;
    }

    private static string ToStoragePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return "system:" + fullPath;
    }

    private static string? ToOptionalStoragePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : ToStoragePath(path);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  WorldUpgradeTool inspect <world-directory>");
        Console.WriteLine("  WorldUpgradeTool plan <world-directory>");
        Console.WriteLine("  WorldUpgradeTool upgrade <source-world-directory> [output-world-directory]");
    }
}
