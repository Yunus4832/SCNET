namespace Engine.Core;

/// <summary>
///     运行模式
/// </summary>
public static class RunMode
{
    public static RunModeType Value = RunModeType.Gui;
}

/// <summary>
///     运行模式枚举
/// </summary>
public enum RunModeType
{
    /// <summary>
    ///     图形客户端
    /// </summary>
    Gui = 0,

    /// <summary>
    ///     无头服务端
    /// </summary>
    HeadlessServer = 1,
}
