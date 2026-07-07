namespace Game.Managers;

public static class VersionsManager
{
    /// <summary>
    /// 游戏名称
    /// </summary>
    public const string GameName = "SCNET";

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
        var assemblyName = new AssemblyName(typeof(VersionsManager).GetTypeInfo().Assembly.FullName!);
        Version =
            $"{assemblyName.Version?.Major}.{assemblyName.Version?.Minor}.{assemblyName.Version?.Build}.{assemblyName.Version?.Revision}";
        Title =$"{GameName}-{Version}";
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
}
