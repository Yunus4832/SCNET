namespace WorldUpgradeTool.Core;

internal interface IWorldMaintenanceStep
{
    string Id { get; }

    string DisplayName { get; }

    WorldStepKind Kind { get; }

    int Order { get; }

    bool IsApplicable(WorldContext context);

    void Execute(WorldContext context);
}
