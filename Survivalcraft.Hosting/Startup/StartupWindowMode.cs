namespace Game.Startup;

/// <summary>描述宿主请求的窗口呈现方式，独立于具体窗口后端。</summary>
public enum StartupWindowMode
{
    Resizable = 0,

    Fixed = 1,

    Borderless = 2,

    Fullscreen = 3
}
