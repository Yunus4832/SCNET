namespace WorldUpgradeTool.Core;

internal sealed class WorldMaintenanceRunner
{
    private readonly WorldMaintenancePlanner _planner = new();

    public WorldMaintenancePlan PlanUpgrade(string directoryName)
    {
        var context = new WorldContext(directoryName);
        return _planner.CreateUpgradePlan(context);
    }

    public void Upgrade(string directoryName)
    {
        var context = new WorldContext(directoryName);
        var executedSteps = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            var plan = _planner.CreateUpgradePlan(context);
            var nextStep = plan.Steps.FirstOrDefault(s => !executedSteps.Contains(s.Id));
            if (nextStep == null)
            {
                return;
            }

            Console.WriteLine($"[{nextStep.Kind}] {nextStep.DisplayName}");
            nextStep.Execute(context);
            executedSteps.Add(nextStep.Id);
            context.RefreshInspection();
        }
    }
}
