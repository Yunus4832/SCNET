namespace WorldUpgradeTool.Core;

internal sealed class WorldMaintenancePlan
{
    public WorldMaintenancePlan(IEnumerable<IWorldMaintenanceStep> steps)
    {
        Steps = steps.OrderBy(s => s.Order).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<IWorldMaintenanceStep> Steps { get; }
}
