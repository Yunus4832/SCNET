namespace Engine.Core;

/// <summary>
/// 渲染同步策略
/// </summary>
public enum PresentationIntervalEnum
{
    /// <summary>
    /// 关闭垂直同步，无限制
    /// </summary>
    Off = 0,

    /// <summary>
    /// 垂直同步
    /// </summary>
    On = 1,

    /// <summary>
    /// 半垂直同步
    /// </summary>
    Half = 2,
}
