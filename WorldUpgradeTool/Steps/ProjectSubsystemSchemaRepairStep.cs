using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using WorldUpgradeTool.Core;

namespace WorldUpgradeTool.Steps;

internal sealed class ProjectSubsystemSchemaRepairStep : IWorldMaintenanceStep
{
    private static readonly string[] _knownObsoleteSubsystemNames =
    [
        "Teams"
    ];

    public string Id => "repair.project-subsystem-schema";

    public string DisplayName => "Remove obsolete project subsystem data";

    public WorldStepKind Kind => WorldStepKind.Repair;

    public int Order => 450;

    public bool IsApplicable(WorldContext context) =>
        context.Inspection.HasProjectXml;

    public void Execute(WorldContext context)
    {
        var projectPath = Storage.CombinePaths(context.DirectoryName, "Project.xml");
        if (!Storage.FileExists(projectPath))
        {
            return;
        }

        XElement projectNode;
        using (var stream = Storage.OpenFile(projectPath, OpenFileMode.Read))
        {
            projectNode = XmlUtils.LoadXmlFromStream(stream, null, true);
        }

        var subsystemsNode = projectNode.Element("Subsystems");
        if (subsystemsNode == null)
        {
            return;
        }

        var allowedSubsystemNames = TryLoadCurrentSubsystemNames();
        var removedCount = 0;
        foreach (var subsystemNode in subsystemsNode.Elements("Values").ToArray())
        {
            var name = XmlUtils.GetAttributeValue(subsystemNode, "Name", string.Empty);
            var isObsolete = allowedSubsystemNames != null
                ? !allowedSubsystemNames.Contains(name)
                : _knownObsoleteSubsystemNames.Contains(name, StringComparer.Ordinal);
            if (!isObsolete)
            {
                continue;
            }

            subsystemNode.Remove();
            removedCount++;
        }

        if (removedCount == 0)
        {
            return;
        }

        using (var stream = Storage.OpenFile(projectPath, OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(projectNode, stream, null, true);
        }

        Console.WriteLine(
            $"Removed {removedCount} obsolete project subsystem entr{(removedCount == 1 ? "y" : "ies")}.");
    }

    private static HashSet<string>? TryLoadCurrentSubsystemNames()
    {
        var databasePath = FindDatabasePath();
        if (databasePath == null)
        {
            return null;
        }

        var databaseNode = XElement.Load(databasePath);
        return databaseNode.Descendants("MemberSubsystemTemplate")
            .Select(e => (string?)e.Attribute("Name"))
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? FindDatabasePath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Content", "Assets", "Database.xml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Content", "Assets", "Database.xml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
