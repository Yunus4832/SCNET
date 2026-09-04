using System.Globalization;
using System.Text;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using WorldUpgradeTool.TerrainSerializers;

namespace WorldUpgradeTool;

internal static class BlockEntityConsistencyRepair
{
    private const int _craftingTableBlockIndex = 27;
    private const int _chestBlockIndex = 45;
    private const int _furnaceBlockIndex = 64;
    private const int _litFurnaceBlockIndex = 65;
    private const int _dispenserBlockIndex = 216;

    private static readonly Dictionary<int, BlockEntityTemplate> _templatesByBlockIndex = new()
    {
        [_craftingTableBlockIndex] = new BlockEntityTemplate("7fa6384c-5fa2-4df6-bb98-5878b645f215", "CraftingTable"),
        [_chestBlockIndex] = new BlockEntityTemplate("08550017-af17-4955-81fa-aafaf97b92bd", "Chest"),
        [_furnaceBlockIndex] = new BlockEntityTemplate("f4a43056-d37d-455f-9a43-803260a915a9", "Furnace"),
        [_litFurnaceBlockIndex] = new BlockEntityTemplate("f4a43056-d37d-455f-9a43-803260a915a9", "Furnace"),
        [_dispenserBlockIndex] = new BlockEntityTemplate("4f1a989d-f12c-4ed5-9334-eacf21815b74", "Dispenser")
    };

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

        var existingBlockEntities = GetExistingBlockEntityCoordinates(projectNode);
        var requiredBlockEntities = FindRequiredBlockEntities(directoryName);
        var entitiesNode = projectNode.Element("Entities");
        if (entitiesNode == null)
        {
            entitiesNode = new XElement("Entities");
            projectNode.Add(entitiesNode);
        }

        var nextEntityId = GetNextEntityId(entitiesNode);
        var repairedCount = 0;
        foreach (var (coordinates, template) in requiredBlockEntities.OrderBy(p => p.Key.X)
                     .ThenBy(p => p.Key.Z)
                     .ThenBy(p => p.Key.Y))
        {
            if (existingBlockEntities.Contains(coordinates))
            {
                continue;
            }

            entitiesNode.Add(CreateBlockEntityNode(nextEntityId++, template, coordinates));
            existingBlockEntities.Add(coordinates);
            repairedCount++;
        }

        if (repairedCount == 0)
        {
            return;
        }

        using (var stream = Storage.OpenFile(projectPath, OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(projectNode, stream, null, true);
        }

        Console.WriteLine($"Repaired {repairedCount} missing block entity entr{(repairedCount == 1 ? "y" : "ies")}.");
    }

    private static Dictionary<Point3, BlockEntityTemplate> FindRequiredBlockEntities(string directoryName)
    {
        var result = new Dictionary<Point3, BlockEntityTemplate>();
        var chunkCoordinates = EnumerateRegionChunkCoordinates(directoryName).ToArray();
        if (chunkCoordinates.Length == 0)
        {
            return result;
        }

        using var serializer = new TerrainSerializer24(directoryName);
        foreach (var chunkCoord in chunkCoordinates)
        {
            var chunk = new TerrainChunk(null, chunkCoord.X, chunkCoord.Y);
            if (!serializer.LoadChunk(chunk))
            {
                continue;
            }

            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++)
                {
                    for (var y = 0; y < 256; y++)
                    {
                        var contents = Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z));
                        if (!_templatesByBlockIndex.TryGetValue(contents, out var template))
                        {
                            continue;
                        }

                        var coordinates = new Point3(chunk.Origin.X + x, y, chunk.Origin.Y + z);
                        result.TryAdd(coordinates, template);
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<Point2> EnumerateRegionChunkCoordinates(string directoryName)
    {
        var regionsDirectory = Storage.CombinePaths(directoryName, "Regions");
        if (!Storage.DirectoryExists(regionsDirectory))
        {
            yield break;
        }

        foreach (var fileName in Storage.ListFileNames(regionsDirectory))
        {
            if (!TryParseRegionFileName(fileName, out var region))
            {
                continue;
            }

            var path = Storage.CombinePaths(regionsDirectory, fileName);
            using var stream = Storage.OpenFile(path, OpenFileMode.Read);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            if (reader.ReadUInt32() != MakeFourCc("RGN1"))
            {
                Console.WriteLine($"Warning: skipped invalid region file \"{fileName}\".");
                continue;
            }

            for (var i = 0; i < 256; i++)
            {
                var offset = reader.ReadInt32();
                var size = reader.ReadInt32();
                if (offset <= 0 || size <= 0)
                {
                    continue;
                }

                var localX = i & 0xF;
                var localZ = i >> 4;
                yield return new Point2(region.X * 16 + localX, region.Y * 16 + localZ);
            }
        }
    }

    private static HashSet<Point3> GetExistingBlockEntityCoordinates(XElement projectNode)
    {
        var result = new HashSet<Point3>();
        foreach (var entityNode in projectNode.Element("Entities")?.Elements("Entity") ?? [])
        {
            var blockEntityNode = entityNode.Elements("Values")
                .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "BlockEntity");
            var coordinatesValueNode = blockEntityNode?.Elements("Value")
                .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Coordinates");
            var value = coordinatesValueNode != null
                ? XmlUtils.GetAttributeValue(coordinatesValueNode, "Value", string.Empty)
                : string.Empty;
            if (TryParsePoint3(value, out var coordinates))
            {
                result.Add(coordinates);
            }
        }

        return result;
    }

    private static XElement CreateBlockEntityNode(int id, BlockEntityTemplate template, Point3 coordinates)
    {
        var entityNode = new XElement("Entity");
        XmlUtils.SetAttributeValue(entityNode, "Id", id);
        XmlUtils.SetAttributeValue(entityNode, "Guid", template.Guid);
        XmlUtils.SetAttributeValue(entityNode, "Name", template.Name);

        var blockEntityNode = new XElement("Values");
        XmlUtils.SetAttributeValue(blockEntityNode, "Name", "BlockEntity");
        entityNode.Add(blockEntityNode);

        var coordinatesNode = new XElement("Value");
        XmlUtils.SetAttributeValue(coordinatesNode, "Name", "Coordinates");
        XmlUtils.SetAttributeValue(coordinatesNode, "Type", "Point3");
        XmlUtils.SetAttributeValue(coordinatesNode, "Value", FormatPoint3(coordinates));
        blockEntityNode.Add(coordinatesNode);

        return entityNode;
    }

    private static int GetNextEntityId(XElement entitiesNode)
    {
        var maxId = 0;
        foreach (var entityNode in entitiesNode.Elements("Entity"))
        {
            maxId = MathUtils.Max(maxId, XmlUtils.GetAttributeValue(entityNode, "Id", 0));
        }

        return maxId + 1;
    }

    private static bool TryParseRegionFileName(string fileName, out Point2 region)
    {
        region = default;
        var name = Storage.GetFileNameWithoutExtension(fileName);
        if (!name.StartsWith("Region ", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = name["Region ".Length..].Split(',');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
        {
            return false;
        }

        region = new Point2(x, z);
        return true;
    }

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

    private static string FormatPoint3(Point3 point) =>
        string.Create(CultureInfo.InvariantCulture, $"{point.X},{point.Y},{point.Z}");

    private static uint MakeFourCc(string s) =>
        s[0] | ((uint)s[1] << 8) | ((uint)s[2] << 16) | ((uint)s[3] << 24);

    private sealed record BlockEntityTemplate(string Guid, string Name);
}
