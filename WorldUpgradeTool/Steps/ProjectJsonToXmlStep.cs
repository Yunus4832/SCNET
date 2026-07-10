using WorldUpgradeTool.Core;

namespace WorldUpgradeTool.Steps;

internal sealed class ProjectJsonToXmlStep : IWorldMaintenanceStep
{
    public string Id => "normalize.project-json-to-xml";

    public string DisplayName => "Convert legacy Project.json to Project.xml";

    public WorldStepKind Kind => WorldStepKind.NormalizeFormat;

    public int Order => 100;

    public bool IsApplicable(WorldContext context) =>
        !context.Inspection.HasProjectXml && context.Inspection.HasProjectJson;

    public void Execute(WorldContext context)
    {
        ProjectFormatNormalizer.EnsureProjectXml(context.DirectoryName);
    }
}
