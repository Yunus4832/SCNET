namespace Engine.FileStorage;

public abstract class RunPath
{
    public const string ExternalPath = "external:";

    public const string GameDataPath = "data:";

    public const string ConfigPath = "config:";

    /// <summary>
    /// 获取实际运行路径
    /// </summary>
    public static string GetOperatingPath() => AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// 获取 EXE 或 dll 所在路径(包含文件自身路径)
    /// </summary>
    public static string GetExecutablePath() => Assembly.GetExecutingAssembly().Location;

    /// <summary>
    /// 获取运行入口路径(用命令行或者其他程序调用时调用者目录)
    /// </summary>
    public static string GetEntryPath() => AppContext.BaseDirectory;

    /// <summary>
    /// 获取环境变量 path 多个路径用分号分隔
    /// </summary>
    public static string GetEnvironmentPath()
    {
        return Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? string.Empty;
    }
}
