using WorldUpgradeTool.Core;

namespace WorldUpgradeTool;

internal static class WorldUpgradeManager
{
    public const string TargetProjectFormatVersion = "SCNET-1";

    public const string TargetTerrainStorageVersion = "2.4";

    public static string ReadProjectVersion(string directoryName)
    {
        ProjectFormatNormalizer.EnsureProjectXml(directoryName);
        return new WorldInspector().Inspect(directoryName).ProjectVersion;
    }

    public static string ReadWorldName(string directoryName)
    {
        ProjectFormatNormalizer.EnsureProjectXml(directoryName);
        return new WorldInspector().Inspect(directoryName).WorldName;
    }

    public static void UpgradeWorld(string directoryName)
    {
        new WorldMaintenanceRunner().Upgrade(directoryName);
    }
}
