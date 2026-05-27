using System.Xml.Linq;

using Engine.Serialization;

using EntitySystem.XmlUtilities;

using Game.VersionConverts;

namespace Game.Managers;

public static class VersionsManager
{
    public const string GameName = "SCNET";

    public const string SerializationVersion = "2.4";

    private static readonly List<VersionConverter> _versionConverters;

#if ANDROID
    public static Platform Platform => Platform.Android;
#endif
#if DESKTOP
    public static Platform Platform => Platform.Desktop;
#endif

    public static BuildConfiguration BuildConfiguration => BuildConfiguration.Release;

    public static string Title { get; }

    public static string Version { get; set; }

    public static string ServerVersion { get; set; } = "0.0.0.1";

    public static string LastLaunchedVersion { get; set; } = string.Empty;

    static VersionsManager()
    {
        _versionConverters = [];
        var assemblyName = new AssemblyName(typeof(VersionsManager).GetTypeInfo().Assembly.FullName!);
        Version =
            $"{assemblyName.Version?.Major}.{assemblyName.Version?.Minor}.{assemblyName.Version?.Build}.{assemblyName.Version?.Revision}-Server{ServerVersion}";
        Title =$"{GameName}-{ServerVersion}";
        var array = TypeCache.LoadedAssemblies.ToArray();
        foreach (var arrayItem in array)
        foreach (var definedType in arrayItem.DefinedTypes)
        {
            if (definedType is { IsAbstract: false, IsInterface: false } &&
                typeof(VersionConverter).GetTypeInfo().IsAssignableFrom(definedType))
            {
                var item = (VersionConverter)Activator.CreateInstance(definedType.AsType())!;
                _versionConverters.Add(item);
            }
        }
    }

    public static void Initialize()
    {
        LastLaunchedVersion = SettingsManager.LastLaunchedVersion;
        SettingsManager.LastLaunchedVersion = Version;
        if (Version != LastLaunchedVersion)
        {
            AnalyticsManager.LogEvent("[VersionsManager] Upgrade game",
                new AnalyticsParameter("LastVersion", LastLaunchedVersion),
                new AnalyticsParameter("CurrentVersion", Version));
        }
    }

    public static void UpgradeProjectXml(XElement projectNode)
    {
        var attributeValue = XmlUtils.GetAttributeValue(projectNode, "Version", "1.0");
        if (attributeValue == SerializationVersion)
        {
            return;
        }

        foreach (var item in FindTransform(attributeValue, SerializationVersion, _versionConverters, 0) ??
                             throw new InvalidOperationException(
                                 $"Cannot find conversion path from version \"{attributeValue}\" to version \"{SerializationVersion}\""))
        {
            Log.Information($"Upgrading world version \"{item.SourceVersion}\" to \"{item.TargetVersion}\".");
            item.ConvertProjectXml(projectNode);
        }

        var attributeValue2 = XmlUtils.GetAttributeValue(projectNode, "Version", "1.0");
        if (attributeValue2 != SerializationVersion)
        {
            throw new InvalidOperationException(
                $"Upgrade produced invalid project version. Expected \"{SerializationVersion}\", found \"{attributeValue2}\".");
        }
    }

    public static void UpgradeWorld(string directoryName)
    {
        var worldInfo = WorldsManager.GetWorldInfo(directoryName);
        if (worldInfo is null)
        {
            return;
        }

        if (worldInfo.SerializationVersion == SerializationVersion)
        {
            return;
        }

        ProgressManager.UpdateProgress($"Upgrading World To {SerializationVersion}", 0f);
        foreach (var item in
                 FindTransform(worldInfo.SerializationVersion, SerializationVersion, _versionConverters, 0) ??
                 throw new InvalidOperationException(
                     $"Cannot find conversion path from version \"{worldInfo.SerializationVersion}\" to version \"{SerializationVersion}\""))
        {
            Log.Information($"Upgrading world version \"{item.SourceVersion}\" to \"{item.TargetVersion}\".");
            item.ConvertWorld(directoryName);
        }

        var worldInfo2 = WorldsManager.GetWorldInfo(directoryName);
        if (worldInfo2 != null && worldInfo2.SerializationVersion != SerializationVersion)
        {
            throw new InvalidOperationException(
                $"Upgrade produced invalid project version. Expected \"{SerializationVersion}\", found \"{worldInfo2.SerializationVersion}\".");
        }

        AnalyticsManager.LogEvent("[VersionConverter] Upgrade world",
            new AnalyticsParameter("SourceVersion", worldInfo.SerializationVersion),
            new AnalyticsParameter("TargetVersion", SerializationVersion));
    }

    public static int CompareVersions(string v1, string v2)
    {
        var array = v1.Split('.');
        var array2 = v2.Split('.');
        for (var i = 0; i < MathUtils.Min(array.Length, array2.Length); i++)
        {
            var num = !int.TryParse(array[i], out var result) || !int.TryParse(array2[i], out var result2)
                ? string.CompareOrdinal(array[i], array2[i])
                : result - result2;
            if (num != 0)
            {
                return num;
            }
        }

        return array.Length - array2.Length;
    }

    private static List<VersionConverter> FindTransform(
        string sourceVersion,
        string targetVersion,
        IEnumerable<VersionConverter> converters,
        int depth
    )
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

        List<VersionConverter> result = [];
        var num = 2147483647;
        var versionConverters = converters as VersionConverter[] ?? converters.ToArray();
        foreach (var converter in versionConverters)
        {
            if (converter.SourceVersion == sourceVersion)
            {
                var list = FindTransform(converter.TargetVersion, targetVersion, versionConverters, depth + 1);
                if (list.Count >= num)
                {
                    continue;
                }

                num = list.Count;
                list.Insert(0, converter);
                result = list;
            }
        }

        return result;
    }
}
