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
        // TODO: Add a player-persistence migration step before project schema cleanup.
        // It must reconcile legacy Players/Entities, OfflinePlayerEntities, and external
        // PlayerEntities JSON/DAT records into the authoritative Players/OfflinePlayers
        // project structure. Do not implement this until the historical layouts and
        // deterministic conflict-resolution rules have been catalogued.
        new ProjectSubsystemSchemaRepairStep(),
        new BlockEntityConsistencyRepairStep(),
        new BasicWorldValidationStep()
    ];

    public WorldMaintenancePlan CreateUpgradePlan(WorldContext context)
    {
        return new WorldMaintenancePlan(_steps.Where(s => s.IsApplicable(context)));
    }
}
