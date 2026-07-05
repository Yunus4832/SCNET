using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using Game.VersionConverts;

namespace WorldUpgradeTool;

internal static class WorldUpgradeManager
{
    public const string TargetWorldSerializationVersion = "2.4";

    private static readonly List<VersionConverter> s_versionConverters = typeof(WorldUpgradeManager).Assembly
        .GetTypes()
        .Where(t => !t.IsAbstract && !t.IsInterface && typeof(VersionConverter).IsAssignableFrom(t))
        .Select(t => (VersionConverter)Activator.CreateInstance(t)!)
        .OrderBy(c => c.SourceVersion, StringComparer.Ordinal)
        .ToList();

    public static string ReadWorldVersion(string directoryName)
    {
        var projectNode = LoadProjectXml(directoryName);
        return XmlUtils.GetAttributeValue(projectNode, "Version", "1.0");
    }

    public static string ReadWorldName(string directoryName)
    {
        var projectNode = LoadProjectXml(directoryName);
        var gameInfoNode = projectNode.Element("Subsystems")?.Elements()
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "GameInfo");
        var worldSettingsNode = gameInfoNode?.Elements("Values")
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "WorldSettings");
        var nameNode = worldSettingsNode?.Elements("Value")
            .FirstOrDefault(e => XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Name");
        return nameNode != null ? XmlUtils.GetAttributeValue(nameNode, "Value", "<unnamed>") : "<unnamed>";
    }

    public static void UpgradeWorld(string directoryName)
    {
        var version = ReadWorldVersion(directoryName);
        if (version == TargetWorldSerializationVersion)
        {
            return;
        }

        var transforms = FindTransform(version, TargetWorldSerializationVersion, s_versionConverters, 0) ??
                         throw new InvalidOperationException(
                             $"Cannot find conversion path from version \"{version}\" to version \"{TargetWorldSerializationVersion}\".");

        foreach (var converter in transforms)
        {
            Console.WriteLine($"Upgrading world version {converter.SourceVersion} -> {converter.TargetVersion}");
            converter.ConvertWorld(directoryName);
        }

        var upgradedVersion = ReadWorldVersion(directoryName);
        if (upgradedVersion != TargetWorldSerializationVersion)
        {
            throw new InvalidOperationException(
                $"Upgrade produced invalid project version. Expected \"{TargetWorldSerializationVersion}\", found \"{upgradedVersion}\".");
        }
    }

    private static XElement LoadProjectXml(string directoryName)
    {
        var path = Storage.CombinePaths(directoryName, "Project.xml");
        using var stream = Storage.OpenFile(path, OpenFileMode.Read);
        return XmlUtils.LoadXmlFromStream(stream, null, true);
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
