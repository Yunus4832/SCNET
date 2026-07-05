using System.Xml.Linq;

using Engine.Serialization;

using EntitySystem.XmlUtilities;

using Game.VersionConverts;

namespace Game.Managers;

public static class VersionsManager
{
    /// <summary>
    /// 游戏名称
    /// </summary>
    public const string GameName = "SCNET";

    /// <summary>
    /// 地形序列化版本号
    /// </summary>
    public const string WorldSerializationVersion = "2.4";

    /// <summary>
    /// 版本转换器列表
    /// </summary>
    private static readonly List<VersionConverter> _versionConverters;

    /// <summary>
    /// 构建配置
    /// </summary>
    public static BuildConfiguration BuildConfiguration => BuildConfiguration.Release;

    /// <summary>
    /// 标题
    /// </summary>
    public static string Title { get; }

    /// <summary>
    /// 版本号
    /// </summary>
    public static string Version { get; set; }

    /// <summary>
    /// 联机协议版本号
    /// </summary>
    public static string ProtocolVersion { get; set; } = "0.0.0.1";

    static VersionsManager()
    {
        _versionConverters = [];
        var assemblyName = new AssemblyName(typeof(VersionsManager).GetTypeInfo().Assembly.FullName!);
        Version =
            $"{assemblyName.Version?.Major}.{assemblyName.Version?.Minor}.{assemblyName.Version?.Build}.{assemblyName.Version?.Revision}";
        Title =$"{GameName}-{Version}";
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

    public static void UpgradeProjectXml(XElement projectNode)
    {
        var attributeValue = XmlUtils.GetAttributeValue(projectNode, "Version", "1.0");
        if (attributeValue == WorldSerializationVersion)
        {
            return;
        }

        foreach (var item in FindTransform(attributeValue, WorldSerializationVersion, _versionConverters, 0) ??
                             throw new InvalidOperationException(
                                 $"Cannot find conversion path from version \"{attributeValue}\" to version \"{WorldSerializationVersion}\""))
        {
            Log.Information($"Upgrading world version \"{item.SourceVersion}\" to \"{item.TargetVersion}\".");
            item.ConvertProjectXml(projectNode);
        }

        var attributeValue2 = XmlUtils.GetAttributeValue(projectNode, "Version", "1.0");
        if (attributeValue2 != WorldSerializationVersion)
        {
            throw new InvalidOperationException(
                $"Upgrade produced invalid project version. Expected \"{WorldSerializationVersion}\", found \"{attributeValue2}\".");
        }
    }

    public static void UpgradeWorld(string directoryName)
    {
        var worldInfo = WorldsManager.GetWorldInfo(directoryName);
        if (worldInfo is null)
        {
            return;
        }

        if (worldInfo.SerializationVersion == WorldSerializationVersion)
        {
            return;
        }

        ProgressManager.UpdateProgress($"Upgrading World To {WorldSerializationVersion}", 0f);
        foreach (var item in
                 FindTransform(worldInfo.SerializationVersion, WorldSerializationVersion, _versionConverters, 0) ??
                 throw new InvalidOperationException(
                     $"Cannot find conversion path from version \"{worldInfo.SerializationVersion}\" to version \"{WorldSerializationVersion}\""))
        {
            Log.Information($"Upgrading world version \"{item.SourceVersion}\" to \"{item.TargetVersion}\".");
            item.ConvertWorld(directoryName);
        }

        var worldInfo2 = WorldsManager.GetWorldInfo(directoryName);
        if (worldInfo2 != null && worldInfo2.SerializationVersion != WorldSerializationVersion)
        {
            throw new InvalidOperationException(
                $"Upgrade produced invalid project version. Expected \"{WorldSerializationVersion}\", found \"{worldInfo2.SerializationVersion}\".");
        }
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
