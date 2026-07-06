using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using Game.VersionConverts;

namespace WorldUpgradeTool;

internal static class WorldUpgradeManager
{
    public const string TargetProjectFormatVersion = "SCNET-1";

    public const string TargetTerrainStorageVersion = "2.4";

    private static readonly List<VersionConverter> _sVersionConverters = typeof(WorldUpgradeManager).Assembly
        .GetTypes()
        .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(VersionConverter).IsAssignableFrom(t))
        .Select(t => (VersionConverter)Activator.CreateInstance(t)!)
        .OrderBy(c => c.SourceVersion, StringComparer.Ordinal)
        .ToList();

    public static string ReadProjectVersion(string directoryName)
    {
        var projectNode = LoadProjectXml(directoryName);
        return XmlUtils.GetAttributeValue(projectNode, "Version", "1.0");
    }

    public static string ReadWorldName(string directoryName)
    {
        var projectNode = LoadProjectXml(directoryName);
        var gameInfoNode = projectNode.Element("Subsystems")?.Elements()
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "GameInfo");
        var nameNode = gameInfoNode?.Elements("Value")
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "WorldName");
        return nameNode != null ? XmlUtils.GetAttributeValue(nameNode, "Value", "<unnamed>") : "<unnamed>";
    }

    public static void UpgradeWorld(string directoryName)
    {
        var version = ReadProjectVersion(directoryName);
        if (version == TargetProjectFormatVersion)
        {
            RepairProjectMetadata(directoryName);
            return;
        }

        if (version != TargetTerrainStorageVersion)
        {
            var transforms = FindTransform(version, TargetTerrainStorageVersion, _sVersionConverters, 0) ??
                             throw new InvalidOperationException(
                                 $"Cannot find conversion path from version \"{version}\" to version \"{TargetTerrainStorageVersion}\".");

            foreach (var converter in transforms)
            {
                Console.WriteLine(
                    $"Upgrading terrain storage version {converter.SourceVersion} -> {converter.TargetVersion}");
                converter.ConvertWorld(directoryName);
            }
        }

        var upgradedVersion = ReadProjectVersion(directoryName);
        if (upgradedVersion != TargetTerrainStorageVersion)
        {
            throw new InvalidOperationException(
                $"Upgrade produced invalid terrain storage version. Expected \"{TargetTerrainStorageVersion}\", found \"{upgradedVersion}\".");
        }

        StampProjectMetadata(directoryName, version);
    }

    private static XElement LoadProjectXml(string directoryName)
    {
        var path = Storage.CombinePaths(directoryName, "Project.xml");
        using var stream = Storage.OpenFile(path, OpenFileMode.Read);
        return XmlUtils.LoadXmlFromStream(stream, null, true);
    }

    private static void SaveProjectXml(string directoryName, XElement projectNode)
    {
        var path = Storage.CombinePaths(directoryName, "Project.xml");
        using var stream = Storage.OpenFile(path, OpenFileMode.Create);
        XmlUtils.SaveXmlToStream(projectNode, stream, null, true);
    }

    private static void StampProjectMetadata(string directoryName, string sourceVersion)
    {
        var projectNode = LoadProjectXml(directoryName);
        var terrainGenerationMode = GetWorldSettingValue(projectNode, "TerrainGenerationMode", "Continent");
        XmlUtils.SetAttributeValue(projectNode, "Version", TargetProjectFormatVersion);
        RepairProjectMetadata(projectNode, Storage.GetFileName(directoryName));
        SetGameInfoValue(projectNode, "TerrainGenerationMode",
            ResolveTerrainGenerationMode(sourceVersion, terrainGenerationMode));
        SaveProjectXml(directoryName, projectNode);
    }

    private static void RepairProjectMetadata(string directoryName)
    {
        var projectNode = LoadProjectXml(directoryName);
        XmlUtils.SetAttributeValue(projectNode, "Version", TargetProjectFormatVersion);
        RepairProjectMetadata(projectNode, Storage.GetFileName(directoryName));
        SaveProjectXml(directoryName, projectNode);
    }

    private static void RepairProjectMetadata(XElement projectNode, string fallbackWorldName)
    {
        EnsureGameInfoNode(projectNode);
        EnsureGameInfoValue(projectNode, "WorldName", "System.String",
            string.IsNullOrWhiteSpace(fallbackWorldName) ? "RecoveredWorld" : fallbackWorldName);
        EnsureGameInfoValue(projectNode, "WorldSeed", "System.Int32", "0");
        EnsureGameInfoValue(projectNode, "WorldSeedString", "System.String", "0");
        EnsureGameInfoValue(projectNode, "GameMode", "Game.GameMode", "Survival");
        EnsureGameInfoValue(projectNode, "EnvironmentBehaviorMode", "Game.EnvironmentBehaviorMode", "Living");
        EnsureGameInfoValue(projectNode, "TerrainGenerationMode", "Game.TerrainGenerationMode", "Continent");

        var terrainGenerationMode = GetWorldSettingValue(projectNode, "TerrainGenerationMode", "Continent");
        SetGameInfoValue(projectNode, "TerrainGenerationMode",
            NormalizeCurrentTerrainGenerationMode(terrainGenerationMode));
    }

    private static string GetWorldSettingValue(XElement projectNode, string name, string defaultValue)
    {
        var valueNode = GetWorldSettingNode(projectNode, name);
        return valueNode != null ? XmlUtils.GetAttributeValue(valueNode, "Value", defaultValue) : defaultValue;
    }

    private static void EnsureGameInfoValue(XElement projectNode, string name, string type, string value)
    {
        var gameInfoNode = EnsureGameInfoNode(projectNode);
        var valueNode = GetValueNode(gameInfoNode, name);
        if (valueNode != null)
        {
            return;
        }

        valueNode = new XElement("Value");
        XmlUtils.SetAttributeValue(valueNode, "Name", name);
        XmlUtils.SetAttributeValue(valueNode, "Type", type);
        XmlUtils.SetAttributeValue(valueNode, "Value", value);
        gameInfoNode.Add(valueNode);
    }

    private static void SetGameInfoValue(XElement projectNode, string name, string value)
    {
        var worldSettingsNode = GetWorldSettingsNode(projectNode);
        if (worldSettingsNode == null)
        {
            return;
        }

        var valueNode = GetValueNode(worldSettingsNode, name);
        if (valueNode == null)
        {
            valueNode = new XElement("Value");
            XmlUtils.SetAttributeValue(valueNode, "Name", name);
            XmlUtils.SetAttributeValue(valueNode, "Type", "Game.TerrainGenerationMode");
            worldSettingsNode.Add(valueNode);
        }

        XmlUtils.SetAttributeValue(valueNode, "Value", value);
    }

    private static XElement? GetWorldSettingsNode(XElement projectNode)
    {
        var gameInfoNode = projectNode.Element("Subsystems")?.Elements()
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "GameInfo");
        return gameInfoNode;
    }

    private static XElement EnsureGameInfoNode(XElement projectNode)
    {
        var subsystemsNode = projectNode.Element("Subsystems");
        if (subsystemsNode == null)
        {
            subsystemsNode = new XElement("Subsystems");
            projectNode.AddFirst(subsystemsNode);
        }

        var gameInfoNode = subsystemsNode.Elements()
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "GameInfo");
        if (gameInfoNode != null)
        {
            return gameInfoNode;
        }

        gameInfoNode = new XElement("Values");
        XmlUtils.SetAttributeValue(gameInfoNode, "Name", "GameInfo");
        subsystemsNode.Add(gameInfoNode);
        return gameInfoNode;
    }

    private static XElement? GetWorldSettingNode(XElement projectNode, string name)
    {
        var worldSettingsNode = GetWorldSettingsNode(projectNode);
        return worldSettingsNode != null ? GetValueNode(worldSettingsNode, name) : null;
    }

    private static XElement? GetValueNode(XElement worldSettingsNode, string name)
    {
        return worldSettingsNode.Elements("Value")
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == name);
    }

    private static string ResolveTerrainGenerationMode(string sourceVersion, string terrainGenerationMode)
    {
        if (sourceVersion == TargetProjectFormatVersion ||
            CompareVersions(sourceVersion, TargetTerrainStorageVersion) >= 0)
        {
            return NormalizeCurrentTerrainGenerationMode(terrainGenerationMode);
        }

        var suffix = CompareVersions(sourceVersion, "2.1") < 0
            ? "Pre21"
            : sourceVersion switch
            {
                "2.1" => "21",
                "2.2" => "22",
                "2.3" => "23",
                _ => "Pre21"
            };
        return terrainGenerationMode switch
        {
            "Island" => $"LegacyIsland{suffix}",
            "FlatContinent" => $"LegacyFlatContinent{suffix}",
            "FlatIsland" => $"LegacyFlatIsland{suffix}",
            _ => $"LegacyContinent{suffix}"
        };
    }

    private static string NormalizeCurrentTerrainGenerationMode(string terrainGenerationMode)
    {
        return terrainGenerationMode switch
        {
            "Island" or "FlatContinent" or "FlatIsland" => terrainGenerationMode,
            _ => "Continent"
        };
    }

    private static int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.Split('.');
        var parts2 = v2.Split('.');
        for (var i = 0; i < Math.Min(parts1.Length, parts2.Length); i++)
        {
            var result = !int.TryParse(parts1[i], out var n1) || !int.TryParse(parts2[i], out var n2)
                ? string.CompareOrdinal(parts1[i], parts2[i])
                : n1 - n2;
            if (result != 0)
            {
                return result;
            }
        }

        return parts1.Length - parts2.Length;
    }

    private static List<VersionConverter>? FindTransform(
        string sourceVersion,
        string targetVersion,
        IEnumerable<VersionConverter> converters,
        int depth)
    {
        if (depth > 100)
        {
            throw new InvalidOperationException(
                "Too deep recursion when searching for version converters. Check for possible loops in transforms.");
        }

        if (sourceVersion == targetVersion)
        {
            return [];
        }

        List<VersionConverter>? result = null;
        var bestLength = int.MaxValue;
        var converterArray = converters as VersionConverter[] ?? converters.ToArray();
        foreach (var converter in converterArray)
        {
            if (converter.SourceVersion != sourceVersion)
            {
                continue;
            }

            var path = FindTransform(converter.TargetVersion, targetVersion, converterArray, depth + 1);
            if (path == null || path.Count >= bestLength)
            {
                continue;
            }

            bestLength = path.Count;
            path.Insert(0, converter);
            result = path;
        }

        return result;
    }
}
