using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace WorldUpgradeTool.Core;

internal sealed class WorldInspector
{
    public WorldInspection Inspect(string directoryName)
    {
        var projectXmlPath = Storage.CombinePaths(directoryName, "Project.xml");
        var projectJsonPath = Storage.CombinePaths(directoryName, "Project.json");
        var hasProjectXml = Storage.FileExists(projectXmlPath);
        var hasProjectJson = Storage.FileExists(projectJsonPath);

        var inspection = new WorldInspection
        {
            DirectoryName = directoryName,
            DirectoryExists = Storage.DirectoryExists(directoryName),
            HasProjectXml = hasProjectXml,
            HasProjectJson = hasProjectJson,
            HasRegionsDirectory = Storage.DirectoryExists(Storage.CombinePaths(directoryName, "Regions"))
        };

        if (!hasProjectXml)
        {
            return inspection;
        }

        XElement projectNode;
        using (var stream = Storage.OpenFile(projectXmlPath, OpenFileMode.Read))
        {
            projectNode = XmlUtils.LoadXmlFromStream(stream, null, true);
        }

        return new WorldInspection
        {
            DirectoryName = directoryName,
            DirectoryExists = inspection.DirectoryExists,
            HasProjectXml = hasProjectXml,
            HasProjectJson = hasProjectJson,
            HasRegionsDirectory = inspection.HasRegionsDirectory,
            ProjectVersion = XmlUtils.GetAttributeValue(projectNode, "Version", "1.0"),
            WorldName = ReadWorldName(projectNode)
        };
    }

    private static string ReadWorldName(XElement projectNode)
    {
        var gameInfoNode = projectNode.Element("Subsystems")?.Elements()
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "GameInfo");
        var nameNode = gameInfoNode?.Elements("Value")
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "WorldName");
        return nameNode != null ? XmlUtils.GetAttributeValue(nameNode, "Value", "<unnamed>") : "<unnamed>";
    }
}
