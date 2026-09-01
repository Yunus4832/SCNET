using System.Globalization;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using WorldUpgradeTool.TerrainSerializers;

namespace WorldUpgradeTool;

internal static class TerritoryStoneMigrationRepair
{
    private const int _airBlockIndex = 0;
    private const int _bedrockBlockIndex = 1;
    private const int _territoryBlockIndex = 264;
    private const string _territorySubsystemName = "TerritoryBlockBehavior";

    private static readonly string[] _legacySubsystemNames =
    [
        "BedrockBlockBehavior",
        "SubsystemBedrockBlockBehavior"
    ];

    public static void Repair(string directoryName)
    {
        var projectPath = Storage.CombinePaths(directoryName, "Project.xml");
        if (!Storage.FileExists(projectPath))
        {
            return;
        }

        XElement projectNode;
        using (var stream = Storage.OpenFile(projectPath, OpenFileMode.Read))
        {
            projectNode = XmlUtils.LoadXmlFromStream(stream, null, true);
        }

        var territoryCoordinates = GetLegacyTerritoryCoordinates(projectNode);
        var projectChanged = RepairProjectSubsystem(projectNode);
        if (projectChanged)
        {
            using var stream = Storage.OpenFile(projectPath, OpenFileMode.Create);
            XmlUtils.SaveXmlToStream(projectNode, stream, null, true);
            Console.WriteLine("Removed legacy territory stone subsystem data.");
        }

        var removedBlockCount = RemoveLegacyTerrainBlocks(directoryName, territoryCoordinates);
        if (removedBlockCount > 0)
        {
            Console.WriteLine(
                $"Removed {removedBlockCount} legacy territory stone block{(removedBlockCount == 1 ? string.Empty : "s")}.");
        }
    }

    private static bool RepairProjectSubsystem(XElement projectNode)
    {
        var subsystemsNode = projectNode.Element("Subsystems");
        if (subsystemsNode == null)
        {
            return false;
        }

        var changed = false;
        var targetNode = FindValuesNode(subsystemsNode, _territorySubsystemName);
        var legacyNodes = _legacySubsystemNames
            .SelectMany(name => subsystemsNode.Elements("Values")
                .Where(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == name))
            .ToArray();

        foreach (var legacyNode in legacyNodes)
        {
            legacyNode.Remove();
            changed = true;
        }

        if (targetNode != null)
        {
            changed |= NormalizeTerritoryFields(targetNode);
        }

        return changed;
    }

    private static bool NormalizeTerritoryFields(XElement territorySubsystemNode)
    {
        var territoriesNode = FindValuesNode(territorySubsystemNode, "Territoriy");
        if (territoriesNode == null)
        {
            return false;
        }

        var changed = false;
        foreach (var territoryNode in territoriesNode.Elements("Values"))
        {
            changed |= EnsureValueNode(
                territoryNode,
                "ApplyToFriend",
                "bool",
                GetValue(territoryNode, "AllowTeamEnter", "False"));
            changed |= RemoveValueNode(territoryNode, "AllowTeamEnter");
            changed |= RemoveValueNode(territoryNode, "OwnerTeamId");
        }

        return changed;
    }

    private static HashSet<Point3> GetLegacyTerritoryCoordinates(XElement projectNode)
    {
        var result = new HashSet<Point3>();
        var subsystemsNode = projectNode.Element("Subsystems");
        if (subsystemsNode == null)
        {
            return result;
        }

        foreach (var legacySubsystemNode in _legacySubsystemNames
                     .SelectMany(name => subsystemsNode.Elements("Values")
                         .Where(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == name)))
        {
            var territoriesNode = FindValuesNode(legacySubsystemNode, "Territoriy");
            if (territoriesNode == null)
            {
                continue;
            }

            foreach (var territoryNode in territoriesNode.Elements("Values"))
            {
                var coordinateNode = territoryNode.Elements("Value")
                    .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "OwnChunkCoord");
                var value = coordinateNode != null
                    ? XmlUtils.GetAttributeValue(coordinateNode, "Value", string.Empty)
                    : string.Empty;
                if (TryParsePoint3(value, out var point) && point.Y is >= 0 and < 256)
                {
                    result.Add(point);
                }
            }
        }

        return result;
    }

    private static int RemoveLegacyTerrainBlocks(string directoryName, HashSet<Point3> territoryCoordinates)
    {
        if (territoryCoordinates.Count == 0)
        {
            return 0;
        }

        var convertedCount = 0;
        using var serializer = new TerrainSerializer24(directoryName);
        foreach (var group in territoryCoordinates.GroupBy(GetChunkCoordinates))
        {
            var chunk = new TerrainChunk(null, group.Key.X, group.Key.Y);
            if (!serializer.LoadChunk(chunk))
            {
                continue;
            }

            var changed = false;
            foreach (var point in group)
            {
                var localX = point.X & 0xF;
                var localZ = point.Z & 0xF;
                var value = chunk.GetCellValueFast(localX, point.Y, localZ);
                var contents = Terrain.ExtractContents(value);
                if (contents != _bedrockBlockIndex && contents != _territoryBlockIndex)
                {
                    continue;
                }

                chunk.SetCellValueFast(localX, point.Y, localZ, Terrain.ReplaceContents(value, _airBlockIndex));
                chunk.ModificationCounter++;
                convertedCount++;
                changed = true;
            }

            if (changed)
            {
                serializer.SaveChunk(chunk);
            }
        }

        return convertedCount;
    }

    private static XElement? FindValuesNode(XElement parent, string name) =>
        parent.Elements("Values")
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == name);

    private static string GetValue(XElement parent, string name, string defaultValue)
    {
        var node = parent.Elements("Value")
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == name);
        return node != null ? XmlUtils.GetAttributeValue(node, "Value", defaultValue) : defaultValue;
    }

    private static bool EnsureValueNode(XElement parent, string name, string type, string value)
    {
        var node = parent.Elements("Value")
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == name);
        if (node != null)
        {
            return false;
        }

        node = new XElement("Value");
        XmlUtils.SetAttributeValue(node, "Name", name);
        XmlUtils.SetAttributeValue(node, "Type", type);
        XmlUtils.SetAttributeValue(node, "Value", value);
        parent.Add(node);
        return true;
    }

    private static bool RemoveValueNode(XElement parent, string name)
    {
        var nodes = parent.Elements("Value")
            .Where(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == name)
            .ToArray();
        foreach (var node in nodes)
        {
            node.Remove();
        }

        return nodes.Length > 0;
    }

    private static Point2 GetChunkCoordinates(Point3 point) =>
        new(point.X >> 4, point.Z >> 4);

    private static bool TryParsePoint3(string value, out Point3 point)
    {
        point = default;
        var parts = value.Split(',');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
        {
            return false;
        }

        point = new Point3(x, y, z);
        return true;
    }
}
