using WorldUpgradeTool.Core;

namespace WorldUpgradeTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            PrintUsage();
            return 2;
        }

        var command = args[0];
        var worldDirectory = ToStoragePath(args[1]);
        if (!Storage.DirectoryExists(worldDirectory))
        {
            Console.Error.WriteLine($"World directory not found: {args[1]}");
            return 1;
        }

        try
        {
            return command.ToLowerInvariant() switch
            {
                "inspect" => Inspect(worldDirectory),
                "plan" => Plan(worldDirectory),
                "upgrade" => Upgrade(worldDirectory),
                _ => PrintInvalidCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
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

    private static int Upgrade(string worldDirectory)
    {
        var before = new WorldInspector().Inspect(worldDirectory);
        PrintInspection(before);
        Console.WriteLine($"Target project version: {WorldUpgradeManager.TargetProjectFormatVersion}");
        Console.WriteLine($"Target terrain storage version: {WorldUpgradeManager.TargetTerrainStorageVersion}");

        WorldUpgradeManager.UpgradeWorld(worldDirectory);

        var after = new WorldInspector().Inspect(worldDirectory);
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

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  WorldUpgradeTool inspect <world-directory>");
        Console.WriteLine("  WorldUpgradeTool plan <world-directory>");
        Console.WriteLine("  WorldUpgradeTool upgrade <world-directory>");
    }
}
