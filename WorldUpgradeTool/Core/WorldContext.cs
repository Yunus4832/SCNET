namespace WorldUpgradeTool.Core;

internal sealed class WorldContext
{
    private readonly WorldInspector _inspector = new();

    public WorldContext(string directoryName)
    {
        DirectoryName = directoryName;
        RefreshInspection();
    }

    public string DirectoryName { get; }

    public WorldInspection Inspection { get; private set; } = null!;

    public string SourceProjectVersion { get; private set; } = "<unknown>";

    public void RefreshInspection()
    {
        Inspection = _inspector.Inspect(DirectoryName);
        if (SourceProjectVersion == "<unknown>" && Inspection.ProjectVersion != "<unknown>")
        {
            SourceProjectVersion = Inspection.ProjectVersion;
        }
    }
}
