using System.Globalization;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using Game.TerrainSerializers;

namespace WorldUpgradeTool;

internal static class TerritoryStoneMigrationRepair
{
    private const int BedrockBlockIndex = 1;
    private const int TerritoryBlockIndex = 264;
    private const string TerritorySubsystemName = "TerritoryBlockBehavior";

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

        var projectChanged = MigrateProjectSubsystem(projectNode);
        var territoryCoordinates = GetTerritoryCoordinates(projectNode);
        if (projectChanged)
        {
            using var stream = Storage.OpenFile(projectPath, OpenFileMode.Create);
            XmlUtils.SaveXmlToStream(projectNode, stream, null, true);
            Console.WriteLine("Migrated legacy territory stone subsystem data.");
        }

        var convertedBlockCount = MigrateTerrainBlocks(directoryName, territoryCoordinates);
        if (convertedBlockCount > 0)
        {
            Console.WriteLine($"Migrated {convertedBlockCount} legacy territory stone block{(convertedBlockCount == 1 ? string.Empty : "s")}.");
        }
    }

    private static bool MigrateProjectSubsystem(XElement projectNode)
    {
        var subsystemsNode = projectNode.Element("Subsystems");
        if (subsystemsNode == null)
        {
            return false;
        }

        var changed = false;
        var targetNode = FindValuesNode(subsystemsNode, TerritorySubsystemName);
        XElement? copiedLegacyNode = null;
        var legacyNodes = _legacySubsystemNames
            .SelectMany(name => subsystemsNode.Elements("Values")
                .Where(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == name))
            .ToArray();
        if (legacyNodes.Length == 0)
        {
            return false;
        }

        if (targetNode == null)
        {
            targetNode = new XElement(legacyNodes[0]);
            XmlUtils.SetAttributeValue(targetNode, "Name", TerritorySubsystemName);
            legacyNodes[0].AddAfterSelf(targetNode);
            copiedLegacyNode = legacyNodes[0];
            changed = true;
        }

        var targetTerritoriesNode = FindValuesNode(targetNode, "Territoriy");
        foreach (var legacyNode in legacyNodes)
        {
            if (legacyNode != copiedLegacyNode)
            {
                var legacyTerritoriesNode = FindValuesNode(legacyNode, "Territoriy");
                if (legacyTerritoriesNode != null)
                {
                    changed |= MergeTerritories(targetNode, ref targetTerritoriesNode, legacyTerritoriesNode);
                }
            }

            legacyNode.Remove();
            changed = true;
        }

        return changed;
    }

    private static bool MergeTerritories(
        XElement targetNode,
        ref XElement? targetTerritoriesNode,
        XElement legacyTerritoriesNode)
    {
        if (targetTerritoriesNode == null)
        {
            targetNode.Add(new XElement(legacyTerritoriesNode));
            targetTerritoriesNode = FindValuesNode(targetNode, "Territoriy");
            return true;
        }

        var changed = false;
        var existingGuids = targetTerritoriesNode.Elements("Values")
            .Select(GetTerritoryGuid)
            .Where(guid => !string.IsNullOrEmpty(guid))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextIndex = GetNextTerritoryIndex(targetTerritoriesNode);

        foreach (var legacyTerritoryNode in legacyTerritoriesNode.Elements("Values"))
        {
            var guid = GetTerritoryGuid(legacyTerritoryNode);
            if (!string.IsNullOrEmpty(guid) && existingGuids.Contains(guid))
            {
                continue;
            }

            var copiedNode = new XElement(legacyTerritoryNode);
            XmlUtils.SetAttributeValue(copiedNode, "Name", (nextIndex++).ToString(CultureInfo.InvariantCulture));
            targetTerritoriesNode.Add(copiedNode);
            if (!string.IsNullOrEmpty(guid))
            {
                existingGuids.Add(guid);
            }

            changed = true;
        }

        return changed;
    }

    private static HashSet<Point3> GetTerritoryCoordinates(XElement projectNode)
    {
        var result = new HashSet<Point3>();
        var subsystemsNode = projectNode.Element("Subsystems");
        var territorySubsystemNode = subsystemsNode != null
            ? FindValuesNode(subsystemsNode, TerritorySubsystemName)
            : null;
        var territoriesNode = territorySubsystemNode != null
            ? FindValuesNode(territorySubsystemNode, "Territoriy")
            : null;
        if (territoriesNode == null)
        {
            return result;
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

        return result;
    }

    private static int MigrateTerrainBlocks(string directoryName, HashSet<Point3> territoryCoordinates)
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
                if (Terrain.ExtractContents(value) != BedrockBlockIndex)
                {
                    continue;
                }

                chunk.SetCellValueFast(localX, point.Y, localZ, Terrain.ReplaceContents(value, TerritoryBlockIndex));
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

    private static string GetTerritoryGuid(XElement territoryNode)
    {
        var guidNode = territoryNode.Elements("Value")
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Guid");
        return guidNode != null ? XmlUtils.GetAttributeValue(guidNode, "Value", string.Empty) : string.Empty;
    }

    private static int GetNextTerritoryIndex(XElement territoriesNode)
    {
        var nextIndex = 0;
        foreach (var territoryNode in territoriesNode.Elements("Values"))
        {
            var name = XmlUtils.GetAttributeValue(territoryNode, "Name", string.Empty);
            if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                nextIndex = MathUtils.Max(nextIndex, index + 1);
            }
        }

        return nextIndex;
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
