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
        // Historical world maintenance does not migrate identities, character ownership,
        // permissions or social data into the current subject model.
        new ProjectSubsystemSchemaRepairStep(),
        new BlockEntityConsistencyRepairStep(),
        new BasicWorldValidationStep()
    ];

    public WorldMaintenancePlan CreateUpgradePlan(WorldContext context)
    {
        return new WorldMaintenancePlan(_steps.Where(s => s.IsApplicable(context)));
    }
}
