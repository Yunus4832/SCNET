using WorldUpgradeTool.Core;

namespace WorldUpgradeTool.Steps;

internal sealed class TerrainVersionUpgradeStep : IWorldMaintenanceStep
{
    private readonly VersionPathFinder _versionPathFinder = new();

    public string Id => "upgrade.terrain-storage";

    public string DisplayName => "Upgrade terrain storage to the target version";

    public WorldStepKind Kind => WorldStepKind.UpgradeVersion;

    public int Order => 200;

    public bool IsApplicable(WorldContext context)
    {
        var version = context.Inspection.ProjectVersion;
        return context.Inspection.HasProjectXml &&
               version != WorldUpgradeManager.TargetProjectFormatVersion &&
               version != WorldUpgradeManager.TargetTerrainStorageVersion;
    }

    public void Execute(WorldContext context)
    {
        var version = context.Inspection.ProjectVersion;
        var transforms = _versionPathFinder.FindPath(version, WorldUpgradeManager.TargetTerrainStorageVersion) ??
                         throw new InvalidOperationException(
                             $"Cannot find conversion path from version \"{version}\" to version \"{WorldUpgradeManager.TargetTerrainStorageVersion}\".");

        foreach (var converter in transforms)
        {
            Console.WriteLine(
                $"Upgrading terrain storage version {converter.SourceVersion} -> {converter.TargetVersion}");
            converter.ConvertWorld(context.DirectoryName);
        }
    }
}
