using WorldUpgradeTool.Steps;

namespace WorldUpgradeTool.Core;

internal sealed class WorldMaintenancePlanner
{
    private readonly IWorldMaintenanceStep[] _steps =
    [
        new ProjectJsonToXmlStep(),
        new TerrainVersionUpgradeStep(),
        new ProjectMetadataRepairStep(),
        new TerritoryStoneMigrationRepairStep(),
        new BlockEntityConsistencyRepairStep(),
        new BasicWorldValidationStep()
    ];

    public WorldMaintenancePlan CreateUpgradePlan(WorldContext context)
    {
        return new WorldMaintenancePlan(_steps.Where(s => s.IsApplicable(context)));
    }
}
