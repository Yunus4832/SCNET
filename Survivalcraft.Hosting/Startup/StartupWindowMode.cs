namespace Game.Startup;

/// <summary>Window presentation requested by the host, independent of a window backend.</summary>
public enum StartupWindowMode
{
    Resizable = 0,

    Fixed = 1,

    Borderless = 2,

    Fullscreen = 3
}
