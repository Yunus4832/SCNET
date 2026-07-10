using WorldUpgradeTool.Core;

namespace WorldUpgradeTool.Steps;

internal sealed class BasicWorldValidationStep : IWorldMaintenanceStep
{
    public string Id => "validate.basic-world-state";

    public string DisplayName => "Validate basic world state";

    public WorldStepKind Kind => WorldStepKind.Validate;

    public int Order => 900;

    public bool IsApplicable(WorldContext context) =>
        context.Inspection.HasProjectXml;

    public void Execute(WorldContext context)
    {
        if (context.Inspection.ProjectVersion != WorldUpgradeManager.TargetProjectFormatVersion)
        {
            throw new InvalidOperationException(
                $"Upgrade produced invalid project version. Expected \"{WorldUpgradeManager.TargetProjectFormatVersion}\", found \"{context.Inspection.ProjectVersion}\".");
        }
    }
}
