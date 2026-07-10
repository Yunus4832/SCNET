using WorldUpgradeTool.Core;

namespace WorldUpgradeTool.Steps;

internal sealed class TerritoryStoneMigrationRepairStep : IWorldMaintenanceStep
{
    public string Id => "repair.territory-stone-migration";

    public string DisplayName => "Migrate legacy territory stone data";

    public WorldStepKind Kind => WorldStepKind.Repair;

    public int Order => 400;

    public bool IsApplicable(WorldContext context) =>
        context.Inspection.HasProjectXml;

    public void Execute(WorldContext context)
    {
        TerritoryStoneMigrationRepair.Repair(context.DirectoryName);
    }
}
