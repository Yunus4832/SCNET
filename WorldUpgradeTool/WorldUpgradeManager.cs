using WorldUpgradeTool.Core;

namespace WorldUpgradeTool;

internal static class WorldUpgradeManager
{
    public const string TargetProjectFormatVersion = "SCNET-1";

    public const string TargetTerrainStorageVersion = "2.4";

    public static string ReadProjectVersion(string directoryName)
    {
        return new WorldInspector().Inspect(directoryName).ProjectVersion;
    }

    public static string ReadWorldName(string directoryName)
    {
        return new WorldInspector().Inspect(directoryName).WorldName;
    }

    public static string UpgradeWorld(string directoryName)
    {
        return new WorldMaintenanceRunner().Upgrade(directoryName);
    }

    public static string UpgradeWorld(string sourceDirectoryName, string destinationDirectoryName)
    {
        return new WorldMaintenanceRunner().Upgrade(sourceDirectoryName, destinationDirectoryName);
    }
}
