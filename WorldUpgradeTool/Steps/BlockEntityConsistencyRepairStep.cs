using WorldUpgradeTool.Core;

namespace WorldUpgradeTool.Steps;

internal sealed class BlockEntityConsistencyRepairStep : IWorldMaintenanceStep
{
    public string Id => "repair.block-entity-consistency";

    public string DisplayName => "Repair missing block entities";

    public WorldStepKind Kind => WorldStepKind.Repair;

    public int Order => 500;

    public bool IsApplicable(WorldContext context) =>
        context.Inspection.HasProjectXml;

    public void Execute(WorldContext context)
    {
        BlockEntityConsistencyRepair.Repair(context.DirectoryName);
    }
}
