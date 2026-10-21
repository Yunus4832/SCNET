namespace WorldUpgradeTool.Core;

internal sealed class WorldMaintenancePlan(IEnumerable<IWorldMaintenanceStep> steps)
{
    public IReadOnlyList<IWorldMaintenanceStep> Steps { get; } =
        steps.OrderBy(s => s.Order).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
}
